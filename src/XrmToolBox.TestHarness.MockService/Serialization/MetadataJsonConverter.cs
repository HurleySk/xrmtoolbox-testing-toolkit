using System;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Newtonsoft.Json.Linq;

namespace XrmToolBox.TestHarness.MockService.Serialization
{
    public class MetadataJsonConverter
    {
        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static EntityMetadata[] EntityMetadataArrayFromJson(JArray json)
        {
            if (json == null)
                return Array.Empty<EntityMetadata>();

            var result = new EntityMetadata[json.Count];
            for (int i = 0; i < json.Count; i++)
            {
                result[i] = json[i] is JObject obj
                    ? EntityMetadataFromJson(obj)
                    : new EntityMetadata();
            }
            return result;
        }

        public static EntityMetadata EntityMetadataFromJson(JObject json)
        {
            var metadata = new EntityMetadata
            {
                LogicalName = json.Value<string>("logicalName"),
                SchemaName = json.Value<string>("schemaName"),
                DisplayName = LabelFromString(json.Value<string>("displayName")),
                DisplayCollectionName = LabelFromString(json.Value<string>("displayCollectionName")),
                EntitySetName = json.Value<string>("entitySetName"),
            };

            // These properties lack public setters in the SDK — use reflection
            SetPropertyViaReflection(metadata, "PrimaryIdAttribute", json.Value<string>("primaryIdAttribute"));
            SetPropertyViaReflection(metadata, "PrimaryNameAttribute", json.Value<string>("primaryNameAttribute"));

            var objectTypeCode = json.Value<int?>("objectTypeCode");
            if (objectTypeCode.HasValue)
                SetPropertyViaReflection(metadata, "ObjectTypeCode", objectTypeCode);

            var isCustomEntity = json.Value<bool?>("isCustomEntity");
            if (isCustomEntity.HasValue)
                SetPropertyViaReflection(metadata, "IsCustomEntity", isCustomEntity);

            var attributes = json["attributes"] as JArray;
            if (attributes != null)
            {
                var attrArray = new AttributeMetadata[attributes.Count];
                for (int i = 0; i < attributes.Count; i++)
                {
                    attrArray[i] = attributes[i] is JObject attrObj
                        ? AttributeMetadataFromJson(attrObj)
                        : new AttributeMetadata();
                }
                SetPropertyViaReflection(metadata, "Attributes", attrArray);
            }

            return metadata;
        }

        public static AttributeMetadata AttributeMetadataFromJson(JObject json)
        {
            var metadata = new AttributeMetadata
            {
                LogicalName = json.Value<string>("logicalName"),
                SchemaName = json.Value<string>("schemaName"),
                DisplayName = LabelFromString(json.Value<string>("displayName")),
            };

            SetPropertyViaReflection(metadata, "EntityLogicalName", json.Value<string>("entityLogicalName"));

            var attributeTypeStr = json.Value<string>("attributeType");
            if (!string.IsNullOrEmpty(attributeTypeStr) &&
                Enum.TryParse<AttributeTypeCode>(attributeTypeStr, true, out var attrType))
            {
                SetPropertyViaReflection(metadata, "AttributeType", (AttributeTypeCode?)attrType);
            }

            return metadata;
        }

        public static Label LabelFromString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new Label();

            return new Label(text, 1033);
        }

        private static void SetPropertyViaReflection(object target, string propertyName, object value)
        {
            if (value == null)
                return;

            var prop = target.GetType().GetProperty(propertyName, NonPublicInstance);
            if (prop != null)
            {
                prop.SetValue(target, value);
                return;
            }

            // Fallback: try backing field patterns (_propertyName or propertyName)
            var field = target.GetType().GetField("_" + char.ToLower(propertyName[0]) + propertyName.Substring(1), NonPublicInstance)
                     ?? target.GetType().GetField(propertyName, NonPublicInstance);
            field?.SetValue(target, value);
        }
    }
}
