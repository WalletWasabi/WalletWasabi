NUGET_PACKAGES_PATH=$HOME/.nuget/packages
FRAMEWORK=netcoreapp2.0
FILTER=FullyQualifiedName~WalletWasabi.Tests$1


ALTCOVER_VERSION=3.0.466
ALTCOVER_PACKAGE_PATH=$NUGET_PACKAGES_PATH/altcover
ALTCOVER_DLL=$ALTCOVER_PACKAGE_PATH/$ALTCOVER_VERSION/tools/$FRAMEWORK/AltCover.dll

NBITCOIN_VERSION=4.1.1.7
NBITCOIN_PACKAGE_PATH=$NUGET_PACKAGES_PATH/nbitcoin
NBITCOIN_DLL=$NBITCOIN_PACKAGE_PATH/$NBITCOIN_VERSION/lib/$FRAMEWORK/NBitcoin.dll
COVERAGE_WORKING_PATH=./WalletWasabi.Tests/bin/Debug/$FRAMEWORK/

rm -rf __Saved
dotnet add WalletWasabi.Tests package AltCover
dotnet restore && dotnet build -f $FRAMEWORK
cp $NBITCOIN_DLL $COVERAGE_WORKING_PATH

dotnet $ALTCOVER_DLL --save --inplace "-i=$COVERAGE_WORKING_PATH"
dotnet test --no-build --filter $FILTER
dotnet $ALTCOVER_DLL runner --collect "-r=$COVERAGE_WORKING_PATH" --lcovReport=lcov.info
