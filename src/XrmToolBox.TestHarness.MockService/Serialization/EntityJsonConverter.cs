using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XrmToolBox.TestHarness.MockService.Serialization
{
    public class EntityJsonConverter
    {
        public static Entity FromJson(JObject json)
        {
            var logicalName = json.Value<string>("logicalName") ?? "";
            var idStr = json.Value<string>("id");
            var id = string.IsNullOrEmpty(idStr) ? Guid.Empty : Guid.Parse(idStr);

            var entity = new Entity(logicalName, id);

            var attributes = json["attributes"] as JObject;
            if (attributes != null)
            {
                foreach (var prop in attributes.Properties())
                {
                    entity[prop.Name] = ConvertAttributeValue(prop.Value);
                }
            }

            return entity;
        }

        public static EntityCollection CollectionFromJson(JObject json)
        {
            var collection = new EntityCollection();

            var entities = json["entities"] as JArray;
            if (entities != null)
            {
                foreach (var item in entities)
                {
                    if (item is JObject entityJson)
                        collection.Entities.Add(FromJson(entityJson));
                }
            }

            collection.MoreRecords = json.Value<bool?>("moreRecords") ?? false;
            collection.PagingCookie = json.Value<string>("pagingCookie");
            collection.TotalRecordCount = json.Value<int?>("totalRecordCount") ?? -1;

            return collection;
        }

        private static object ConvertAttributeValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            if (token is JObject obj)
            {
                // EntityReference: { "logicalName": "...", "id": "..." }
                if (obj["logicalName"] != null && obj["id"] != null && obj["value"] == null)
                {
                    return new EntityReference(
                        obj.Value<string>("logicalName"),
                        Guid.Parse(obj.Value<string>("id")));
                }

                // OptionSetValue: { "value": 123 }
                if (obj["value"] != null && obj.Count == 1)
                {
                    var val = obj["value"];
                    if (val.Type == JTokenType.Integer)
                        return new OptionSetValue(val.Value<int>());
                    if (val.Type == JTokenType.Float)
                        return new Money(val.Value<decimal>());
                }

                // Money: { "value": 100.00, "type": "money" }
                if (obj["type"] != null && obj.Value<string>("type") == "money")
                {
                    return new Money(obj.Value<decimal>("value"));
                }

                return obj.ToString();
            }

            switch (token.Type)
            {
                case JTokenType.Integer:
                    return token.Value<int>();
                case JTokenType.Float:
                    return token.Value<decimal>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Date:
                    return token.Value<DateTime>();
                case JTokenType.String:
                    var str = token.Value<string>();
                    if (Guid.TryParse(str, out var guid))
                        return guid;
                    return str;
                default:
                    return token.ToString();
            }
        }
    }
}
