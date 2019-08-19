COLLECT_COVERAGE="//p:CollectCoverage=true"
OUTPUT_FORMAT="//p:CoverletOutputFormat=lcov"
FILE_NAME="//p:CoverletOutputName=lcov"

foo bar buz

cd WalletWasabi.Tests
dotnet test --no-build $COLLECT_COVERAGE $OUTPUT_FORMAT $FILE_NAME
