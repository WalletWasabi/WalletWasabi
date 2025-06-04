// This script can be used to migrate a plain text index (prior to commit 1579876) to a SqLite index.
// Usage: Edit inputFilePath and outputDbPath then dotnet fsi migrateIndexerFilters.fsx

#r "nuget: Microsoft.Data.Sqlite"

open System
open System.IO
open Microsoft.Data.Sqlite

let indexServiceDirectory = Path.Combine (Environment.GetEnvironmentVariable("HOME"), ".walletwasabi/indexer/IndexBuilderService")
let inputFilePath = Path.Combine(indexServiceDirectory, "IndexMain.dat")
let outputDbPath = Path.Combine(indexServiceDirectory, "IndexMain.sqlite");
let batchSize = 1000

type Filter = { Height: int; BlockHash: byte[]; Filter: byte[]; BlockTime: int64; PrevBlockHash: byte[] }

let from_big_endian_str (s: string) = Convert.FromHexString(s) |> Array.rev
let from_little_endian_str (s: string) = Convert.FromHexString(s)

let createDatabaseIfNotExists (path: string) =
    if not (File.Exists(path)) then
        let conn = new SqliteConnection($"Data Source={path}")
        conn.Open()
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            CREATE TABLE filter (
                block_height INTEGER NOT NULL PRIMARY KEY,
                block_hash BLOB NOT NULL,
                filter_data BLOB NOT NULL,
                previous_block_hash BLOB NOT NULL,
                epoch_block_time INTEGER NOT NULL
            );
            CREATE INDEX idx_blocks_height ON filter(block_height);
            CREATE INDEX idx_blocks_hash ON filter(block_hash);
        """
        cmd.ExecuteNonQuery() |> ignore
        printfn "Database created."
    else
        printfn "Database already exists. Skipping creation."

let insertFiltersBatch (conn: SqliteConnection) (filters: Filter list) =
    let transaction = conn.BeginTransaction()
    let cmd = conn.CreateCommand()
    cmd.Transaction <- transaction
    cmd.CommandText <- """
        INSERT OR REPLACE INTO filter (block_height, block_hash, filter_data, previous_block_hash, epoch_block_time)
        VALUES (@height, @blockHash, @filter, @prevBlockHash, @blockTime)
    """

    let heightParam = cmd.CreateParameter()
    heightParam.ParameterName <- "@height"
    cmd.Parameters.Add(heightParam) |> ignore

    let blockHashParam = cmd.CreateParameter()
    blockHashParam.ParameterName <- "@blockHash"
    cmd.Parameters.Add(blockHashParam) |> ignore

    let filterParam = cmd.CreateParameter()
    filterParam.ParameterName <- "@filter"
    cmd.Parameters.Add(filterParam) |> ignore

    let prevBlockHashParam = cmd.CreateParameter()
    prevBlockHashParam.ParameterName <- "@prevBlockHash"
    cmd.Parameters.Add(prevBlockHashParam) |> ignore

    let blockTimeParam = cmd.CreateParameter()
    blockTimeParam.ParameterName <- "@blockTime"
    cmd.Parameters.Add(blockTimeParam) |> ignore

    let mutable inserted = 0
    for filter in filters do
        heightParam.Value <- filter.Height
        blockHashParam.Value <- filter.BlockHash
        filterParam.Value <- filter.Filter
        prevBlockHashParam.Value <- filter.PrevBlockHash
        blockTimeParam.Value <- filter.BlockTime
        inserted <- inserted + cmd.ExecuteNonQuery()

    transaction.Commit()
    inserted

let processLine (line: string) =
    let parts = line.Split(':')
    {
        Height = int parts.[0]
        BlockHash = from_big_endian_str parts.[1]
        Filter = from_little_endian_str parts.[2]
        PrevBlockHash = from_big_endian_str parts.[3]
        BlockTime = int64 parts.[4]
    }

let getMaxBlockHeight (conn: SqliteConnection) =
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT MAX(block_height) FROM filter;"
    cmd.ExecuteScalar() :?> int64

createDatabaseIfNotExists outputDbPath

let conn = new SqliteConnection($"Data Source={outputDbPath}")
conn.Open()

let mutable totalProcessed = 0
let mutable totalInserted = 0

let reader = new StreamReader(inputFilePath)
let mutable batch = []

while not reader.EndOfStream do
    let line = reader.ReadLine()
    let filter = processLine line
    batch <- filter :: batch
    totalProcessed <- totalProcessed + 1

    if batch.Length = batchSize || reader.EndOfStream then
        if totalProcessed % 10_000 = 0 then
            printf "."
        let inserted = insertFiltersBatch conn batch
        totalInserted <- totalInserted + inserted
        batch <- []

printfn $"\nCompleted. Total processed: %d{totalProcessed}, Total inserted: %d{totalInserted}"
let maxHeight = getMaxBlockHeight conn
printfn $"Max Block Height in DB: %d{maxHeight}"
