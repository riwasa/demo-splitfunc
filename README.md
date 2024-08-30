## Azure Setup

In Azure, create the following:

- A storage account to store the files to summarize.
    - Standard General Purpose v2
    - Locally-redundant storage (LRS)
    - Hot access tier
    - Container named 'intake' to store the files to summarize
    - Container named 'output' to store the summarized files. 
- A function app to run the function.
    - Linux
    - .NET 8 Isolated
    - Environment variable created under App Settings named 'StorageAccountConnectionString' 
      with a value of the connection string for the storage account created above.
    - Storage account for function state.
    - Application Insights component for monitoring.

## Running the solution locally

To run the solution locally, create a "local.settings.json" file in the same directory as the Program.cs
file. Add the following to the local.settings.json file:

```
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "StorageAccountConnectionString": "[connection-string]"
  }
}
```

Replace "[connection-string]" with the connection string for the storage account where the 
files to summarize are stored.