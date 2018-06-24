COLLECT_COVERAGE="//p:CollectCoverage=true"
OUTPUT_FORMAT="//p:CoverletOutputFormat=lcov"
FILE_NAME="//p:CoverletOutputName=lcov"

dotnet restore && dotnet build

cd WalletWasabi.Tests
dotnet test --no-build $COLLECT_COVERAGE $OUTPUT_FORMAT $FILE_NAME