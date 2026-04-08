using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.TestHarness.MockService;

namespace XrmToolBox.TestHarness
{
    /// <summary>
    /// Wraps any IOrganizationService to record SDK calls via RequestRecorder,
    /// then delegates to the inner service. Used for recording against live connections.
    /// </summary>
    public class RecordingServiceDecorator : IOrganizationService
    {
        private readonly IOrganizationService _inner;
        private readonly RequestRecorder _recorder;

        public RecordingServiceDecorator(IOrganizationService inner, RequestRecorder recorder)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public Guid Create(Entity entity)
        {
            _recorder.Record("Create", entity?.LogicalName, null, true, "live");
            return _inner.Create(entity);
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            _recorder.Record("Retrieve", entityName, null, true, "live");
            return _inner.Retrieve(entityName, id, columnSet);
        }

        public void Update(Entity entity)
        {
            _recorder.Record("Update", entity?.LogicalName, null, true, "live");
            _inner.Update(entity);
        }

        public void Delete(string entityName, Guid id)
        {
            _recorder.Record("Delete", entityName, null, true, "live");
            _inner.Delete(entityName, id);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            _recorder.Record("Execute", null, request?.GetType().FullName, true, "live");
            return _inner.Execute(request);
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var entityName = (query as QueryExpression)?.EntityName;
            _recorder.Record("RetrieveMultiple", entityName, null, true, "live");
            return _inner.RetrieveMultiple(query);
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities)
        {
            _recorder.Record("Associate", entityName, null, true, "live");
            _inner.Associate(entityName, entityId, relationship, relatedEntities);
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities)
        {
            _recorder.Record("Disassociate", entityName, null, true, "live");
            _inner.Disassociate(entityName, entityId, relationship, relatedEntities);
        }
    }
}
