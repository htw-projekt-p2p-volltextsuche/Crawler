## How to setup
1. Make sure you have .NET 5.0 Runtime installed or higher.
2. Clone the repository
3. Edit the appsettings.json and add the required MongoDB connection string. Optionally adjust the indexing interval.
4. Run the application with `dotnet run`

## Docker compose setup
### Persistent mounting points
- The path `/app/local.db` (by default) needs to be persisted to the host for tracking which protocols have already been indexed to work.

### Application settings
Application settings can be changed either by mounting a `appsettings.json` file into the container or by environment variables.

- If mounting a `appsettings.json` file, mount it to `/app/appsettings.json` inside the container.
- If configuring via environment variables, use the names of the option inside the `appsettings.json` as the environment variable name, e.g. `IndexingApiEndpoint` or `Interval`.

## Functionality
The crawler downloads speeches from the official Bundestag website in regular, customizable intervals.

The speeches are processed through seperate executables and the returned results inserted into the P2P network for indexing.

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
    "date": "dd.mm.yyyy",

    // Entire processed content of the speech.
    "text": "string"
}
```