## Functionality
The crawler downloads speeches from the official Bundestag website in regular, customizable intervals.

The speeches are processed through seperate executables and the returned results inserted into the P2P network for indexing. Only the time periods >= 18 are supported. Time periods below 18 will be skipped.

## How to run without Docker
1. Make sure you have .NET 5.0 SDK installed or higher.
1. Clone the repository
1. Build the application with `dotnet publish ./Crawler -c Release`
1. Switch to the build directory `./Crawler/bin/Release/net5.0/publish`
1. Edit `appsettings.json` and add the required MongoDB connection string `MongoConnectionString` aswell as the endpoint to the indexing API `IndexingApiEndpoint`. Optionally adjust the indexing interval.
1. Run the application `./Crawler.exe`

## Running with Docker Compose
### Persistent mounting points
- The path `/app/data/` (by default) needs to be persisted to the host for tracking which protocols have already been indexed to work.

### Application settings
Application settings can be changed either by mounting a `appsettings.json` file into the container or by environment variables.

- If mounting a `appsettings.json` file, mount it to `/app/appsettings.json` inside the container.
- If configuring via environment variables, use the names of the option inside the `appsettings.json` as the environment variable name, e.g. `IndexingApiEndpoint` or `Interval`.

#### Default appsettings.json
```jsonc
{
  // Interval (CRON Expression) in which the Bundestag website will be crawled
  "Interval": "* * * * *",

  // One time delay in seconds before the Crawler will evaluate the interval CRON expression and run according to the given "Interval" schedule
  "InitialDelay": 0,

  // Delay in seconds in between HTTP calls to the indexing api to save a chunk of protocols
  "ChunkDelay": 0,

  // Maximum batch size in which documents will be POSTed to the indexing api endpoint
  "MaximumBatchSize": 5,

  // Database which will be used to save speeches
  "MongoConnectionString": "mongodb://0.0.0.0:8430",
  "MongoDatabase": "crawler",
  "MongoCollection": "protocols",

  // Database that is used to determine which documents of the Bundestag have already been indexed
  "LocalDbConnectionString": "Data Source=data/local.db",

  // Indexing api endpoint (without a trailing slash!)
  "IndexingApiEndpoint": "http://0.0.0.0:8421/api",

  // Timeout of the HTTP request to the api endpoints in seconds
  "IndexingApiTimeout": 300
}
```

## External Specifications
### MongoDB speech document
The crawler connects to a MongoDB database for easy access of extracted speeches for the frontend. The document representing a speech is defined as follows:

```JSON5
{
    // This ID is automatically generated by the crawler, as the official data source does not provide unique IDs for all speeches provided. This is a GUID as per GUID4 specification.
    "id": "uuid",

    // Title of the speech.
    "title": "string",
    
    // The name of the speaker.
    "speaker": "string",

    // Affiliation of the speaker. This can be his/her role in the Bundestag or for example his party.
    "affiliation": "string",

    // Date of the speech in the format dd.mm.yyyy
    "date": "dd-mm-yyyy",

    // Entire processed content of the speech.
    "text": "string"
}
```
