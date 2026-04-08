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
            try
            {
                var result = _inner.Create(entity);
                _recorder.Record("Create", entity?.LogicalName, null, true, "live");
                return result;
            }
            catch (Exception)
            {
                _recorder.Record("Create", entity?.LogicalName, null, false, "live-error");
                throw;
            }
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            try
            {
                var result = _inner.Retrieve(entityName, id, columnSet);
                _recorder.Record("Retrieve", entityName, null, true, "live");
                return result;
            }
            catch (Exception)
            {
                _recorder.Record("Retrieve", entityName, null, false, "live-error");
                throw;
            }
        }

        public void Update(Entity entity)
        {
            try
            {
                _inner.Update(entity);
                _recorder.Record("Update", entity?.LogicalName, null, true, "live");
            }
            catch (Exception)
            {
                _recorder.Record("Update", entity?.LogicalName, null, false, "live-error");
                throw;
            }
        }

        public void Delete(string entityName, Guid id)
        {
            try
            {
                _inner.Delete(entityName, id);
                _recorder.Record("Delete", entityName, null, true, "live");
            }
            catch (Exception)
            {
                _recorder.Record("Delete", entityName, null, false, "live-error");
                throw;
            }
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            try
            {
                var result = _inner.Execute(request);
                _recorder.Record("Execute", null, request?.GetType().FullName, true, "live");
                return result;
            }
            catch (Exception)
            {
                _recorder.Record("Execute", null, request?.GetType().FullName, false, "live-error");
                throw;
            }
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var entityName = (query as QueryExpression)?.EntityName;
            try
            {
                var result = _inner.RetrieveMultiple(query);
                _recorder.Record("RetrieveMultiple", entityName, null, true, "live");
                return result;
            }
            catch (Exception)
            {
                _recorder.Record("RetrieveMultiple", entityName, null, false, "live-error");
                throw;
            }
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities)
        {
            try
            {
                _inner.Associate(entityName, entityId, relationship, relatedEntities);
                _recorder.Record("Associate", entityName, null, true, "live");
            }
            catch (Exception)
            {
                _recorder.Record("Associate", entityName, null, false, "live-error");
                throw;
            }
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship,
            EntityReferenceCollection relatedEntities)
        {
            try
            {
                _inner.Disassociate(entityName, entityId, relationship, relatedEntities);
                _recorder.Record("Disassociate", entityName, null, true, "live");
            }
            catch (Exception)
            {
                _recorder.Record("Disassociate", entityName, null, false, "live-error");
                throw;
            }
        }
    }
}
