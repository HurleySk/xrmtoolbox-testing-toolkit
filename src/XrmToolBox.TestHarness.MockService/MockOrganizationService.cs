using System;
using System.ServiceModel;
using System.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.TestHarness.MockService.Models;

namespace XrmToolBox.TestHarness.MockService
{
    public class MockOrganizationService : IOrganizationService
    {
        private readonly MockDataStore _dataStore;
        private readonly RequestRecorder _recorder;

        public MockOrganizationService(MockDataStore dataStore, RequestRecorder recorder)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public Guid Create(Entity entity)
        {
            var entry = _dataStore.FindMatch("Create", entity.LogicalName);
            var matched = entry != null;

            _recorder.Record("Create", entity.LogicalName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Create", entity.LogicalName);

            return matched ? _dataStore.GetGuidResponse(entry) : Guid.NewGuid();
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            var entry = _dataStore.FindMatch("Retrieve", entityName);
            var matched = entry != null;

            _recorder.Record("Retrieve", entityName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Retrieve", entityName);

            if (matched)
            {
                var result = _dataStore.GetEntityResponse(entry);
                if (result != null)
                {
                    if (result.Id == Guid.Empty)
                        result.Id = id;
                    if (string.IsNullOrEmpty(result.LogicalName))
                        result.LogicalName = entityName;
                    return result;
                }
            }

            return new Entity(entityName, id);
        }

        public void Update(Entity entity)
        {
            var entry = _dataStore.FindMatch("Update", entity.LogicalName);
            var matched = entry != null;

            _recorder.Record("Update", entity.LogicalName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Update", entity.LogicalName);
        }

        public void Delete(string entityName, Guid id)
        {
            var entry = _dataStore.FindMatch("Delete", entityName);
            var matched = entry != null;

            _recorder.Record("Delete", entityName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Delete", entityName);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            // Route Execute(AssociateRequest) to the Associate handler so it matches
            // operation: "Associate" mock entries — plugins may call either service.Associate()
            // or service.Execute(new AssociateRequest(...)) for the same logical operation.
            if (request is AssociateRequest assocReq && assocReq.Target != null)
            {
                Associate(
                    assocReq.Target.LogicalName,
                    assocReq.Target.Id,
                    assocReq.Relationship,
                    assocReq.RelatedEntities);
                return new AssociateResponse();
            }

            if (request is DisassociateRequest disassocReq && disassocReq.Target != null)
            {
                Disassociate(
                    disassocReq.Target.LogicalName,
                    disassocReq.Target.Id,
                    disassocReq.Relationship,
                    disassocReq.RelatedEntities);
                return new DisassociateResponse();
            }

            var entry = _dataStore.FindMatch("Execute", request: request);
            var matched = entry != null;

            _recorder.Record("Execute", null, request.GetType().FullName, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Execute", request.GetType().Name);

            return matched
                ? _dataStore.GetExecuteResponse(entry, request)
                : new OrganizationResponse { ResponseName = request.RequestName };
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            string entityName = null;
            if (query is QueryExpression qe)
                entityName = qe.EntityName;

            var entry = _dataStore.FindMatch("RetrieveMultiple", entityName, query: query);
            var matched = entry != null;

            _recorder.Record("RetrieveMultiple", entityName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("RetrieveMultiple", entityName);

            return matched
                ? _dataStore.GetEntityCollectionResponse(entry)
                : new EntityCollection();
        }

        public void Associate(string entityName, Guid entityId,
            Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var entry = _dataStore.FindMatch("Associate", entityName);
            var matched = entry != null;

            _recorder.Record("Associate", entityName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Associate", entityName);
        }

        public void Disassociate(string entityName, Guid entityId,
            Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var entry = _dataStore.FindMatch("Disassociate", entityName);
            var matched = entry != null;

            _recorder.Record("Disassociate", entityName, null, matched, entry?.Description);
            ApplyDelay(entry);

            if (entry?.Fault != null)
                ThrowFault(entry.Fault);

            if (!matched && _dataStore.Configuration.Settings.ThrowIfUnmatched)
                ThrowUnmatched("Disassociate", entityName);
        }

        private void ApplyDelay(MockResponseEntry entry)
        {
            var delay = entry?.Delay ?? _dataStore.Configuration.Settings.DefaultDelay;
            if (delay > 0)
                Thread.Sleep(delay);
        }

        private static void ThrowFault(MockFault fault)
        {
            var orgFault = new OrganizationServiceFault
            {
                ErrorCode = fault.ErrorCode,
                Message = fault.Message ?? "Mock fault"
            };
            throw new FaultException<OrganizationServiceFault>(orgFault, orgFault.Message);
        }

        private static void ThrowUnmatched(string operation, string entityName)
        {
            var msg = $"No mock data configured for {operation}" +
                      (entityName != null ? $" on entity '{entityName}'" : "");
            var fault = new OrganizationServiceFault
            {
                ErrorCode = -1,
                Message = msg
            };
            throw new FaultException<OrganizationServiceFault>(fault, msg);
        }
    }
}
