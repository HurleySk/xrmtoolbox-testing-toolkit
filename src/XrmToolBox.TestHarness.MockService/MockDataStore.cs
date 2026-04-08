using System;
using System.IO;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XrmToolBox.TestHarness.MockService.Models;
using Microsoft.Xrm.Sdk.Metadata;
using XrmToolBox.TestHarness.MockService.Serialization;

namespace XrmToolBox.TestHarness.MockService
{
    public class MockDataStore
    {
        private string _basePath;
        private MockConfiguration _config;
        private readonly RequestMatcher _matcher = new RequestMatcher();

        public MockConfiguration Configuration => _config;

        public MockDataStore(string jsonFilePath)
        {
            if (string.IsNullOrEmpty(jsonFilePath))
            {
                _config = new MockConfiguration();
                _basePath = null;
                return;
            }

            _basePath = Path.GetDirectoryName(Path.GetFullPath(jsonFilePath));
            Load(jsonFilePath);
        }

        private void Load(string path)
        {
            var json = File.ReadAllText(path);
            _config = JsonConvert.DeserializeObject<MockConfiguration>(json);
        }

        public void Reload(string path)
        {
            _basePath = null; // Will use new path's directory
            Load(path);
        }

        public MockResponseEntry FindMatch(string operation, string entityName = null,
            OrganizationRequest request = null, QueryBase query = null)
        {
            return _matcher.FindMatch(_config.Responses, operation, entityName, request, query);
        }

        public EntityCollection GetEntityCollectionResponse(MockResponseEntry entry)
        {
            var responseJson = GetResponseJson(entry);
            if (responseJson == null)
                return new EntityCollection();

            return EntityJsonConverter.CollectionFromJson(responseJson);
        }

        public Entity GetEntityResponse(MockResponseEntry entry)
        {
            var responseJson = GetResponseJson(entry);
            if (responseJson == null)
                return null;

            return EntityJsonConverter.FromJson(responseJson);
        }

        public Guid GetGuidResponse(MockResponseEntry entry)
        {
            var responseJson = GetResponseJson(entry);
            if (responseJson == null)
                return Guid.NewGuid();

            var idStr = responseJson.Value<string>("id");
            return string.IsNullOrEmpty(idStr) ? Guid.NewGuid() : Guid.Parse(idStr);
        }

        public OrganizationResponse GetExecuteResponse(MockResponseEntry entry, OrganizationRequest request)
        {
            var responseJson = GetResponseJson(entry);
            if (responseJson == null)
                return new OrganizationResponse { ResponseName = request.RequestName };

            var responseTypeName = responseJson.Value<string>("responseType");
            OrganizationResponse response;

            if (!string.IsNullOrEmpty(responseTypeName))
            {
                var responseType = FindType(responseTypeName);
                response = responseType != null
                    ? (OrganizationResponse)Activator.CreateInstance(responseType)
                    : new OrganizationResponse();
            }
            else
            {
                response = new OrganizationResponse();
            }

            response.ResponseName = request.RequestName;

            // Handle entityMetadata (used by RetrieveAllEntitiesResponse, RetrieveEntityResponse, etc.)
            var entityMetadataToken = responseJson["entityMetadata"];
            if (entityMetadataToken is JArray metadataArray)
            {
                response.Results["EntityMetadata"] = MetadataJsonConverter.EntityMetadataArrayFromJson(metadataArray);
            }
            else if (entityMetadataToken is JObject metadataObj)
            {
                response.Results["EntityMetadata"] = MetadataJsonConverter.EntityMetadataFromJson(metadataObj);
            }

            var results = responseJson["results"] as JObject;
            if (results != null)
            {
                foreach (var prop in results.Properties())
                {
                    response.Results[prop.Name] = ConvertResultValue(prop.Value);
                }
            }

            return response;
        }

        private JObject GetResponseJson(MockResponseEntry entry)
        {
            if (entry.Response != null)
                return entry.Response;

            if (!string.IsNullOrEmpty(entry.ResultsFile) && _basePath != null)
            {
                var filePath = Path.Combine(_basePath, entry.ResultsFile);
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JObject.Parse(json);
                }
            }

            return null;
        }

        private static object ConvertResultValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            switch (token.Type)
            {
                case JTokenType.String:
                    var str = token.Value<string>();
                    if (Guid.TryParse(str, out var guid))
                        return guid;
                    return str;
                case JTokenType.Integer:
                    return token.Value<int>();
                case JTokenType.Float:
                    return token.Value<decimal>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                default:
                    return token.ToString();
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
