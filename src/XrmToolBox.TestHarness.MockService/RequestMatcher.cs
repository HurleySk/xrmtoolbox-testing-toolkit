using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.TestHarness.MockService.Models;

namespace XrmToolBox.TestHarness.MockService
{
    public class RequestMatcher
    {
        public MockResponseEntry FindMatch(IList<MockResponseEntry> entries, string operation,
            string entityName = null, OrganizationRequest request = null,
            QueryBase query = null)
        {
            return entries.FirstOrDefault(e => IsMatch(e, operation, entityName, request, query));
        }

        private bool IsMatch(MockResponseEntry entry, string operation,
            string entityName, OrganizationRequest request, QueryBase query)
        {
            if (!string.Equals(entry.Operation, operation, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var criterion in entry.Match)
            {
                if (!MatchesCriterion(criterion.Key, criterion.Value, entityName, request, query))
                    return false;
            }

            return true;
        }

        private bool MatchesCriterion(string key, string value,
            string entityName, OrganizationRequest request, QueryBase query)
        {
            if (value == "*")
                return true;

            switch (key.ToLowerInvariant())
            {
                case "entityname":
                    return string.Equals(entityName, value, StringComparison.OrdinalIgnoreCase);

                case "requesttype":
                    return request != null &&
                           request.GetType().FullName.Equals(value, StringComparison.OrdinalIgnoreCase);

                case "queryexpressionentity":
                    return query is QueryExpression qe &&
                           string.Equals(qe.EntityName, value, StringComparison.OrdinalIgnoreCase);

                case "fetchxmlcontains":
                    return query is FetchExpression fe &&
                           fe.Query != null &&
                           fe.Query.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

                default:
                    return true;
            }
        }
    }
}
