dotnet publish -c debug -r debian-arm64 --self-contained
cd bin\debug\net6.0\debian-arm64

$compress = @{
LiteralPath= "publish"
CompressionLevel = "Fastest"
DestinationPath = "./publish.zip"
}

Compress-Archive @compress -Force

gcloud compute scp publish.zip instance-2:publish.zip