
SETLOCAL
cd %~dp0

kubectl apply -k .

start kubectl port-forward --namespace token-example service/server 6000

dotnet run --project ClientApp -- --environment Development
