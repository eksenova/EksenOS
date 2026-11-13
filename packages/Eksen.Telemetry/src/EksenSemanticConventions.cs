namespace Eksen.Telemetry;

/// <summary>
/// Attribute names shared by spans, metrics and log records. The standard names follow the
/// OpenTelemetry semantic conventions; the <c>eksen.*</c> names are framework specific.
/// </summary>
public static class EksenSemanticConventions
{
    public const string MessagingSystem = "messaging.system";
    public const string MessagingDestinationName = "messaging.destination.name";
    public const string MessagingOperationType = "messaging.operation.type";
    public const string MessagingOperationName = "messaging.operation.name";
    public const string MessagingMessageId = "messaging.message.id";
    public const string MessagingMessageConversationId = "messaging.message.conversation_id";
    public const string MessagingConsumerGroupName = "messaging.consumer.group.name";
    public const string MessagingRabbitMqDestinationRoutingKey = "messaging.rabbitmq.destination.routing_key";
    public const string ErrorType = "error.type";
    public const string DbSystemName = "db.system.name";
    public const string DbOperationName = "db.operation.name";
    public const string DbNamespace = "db.namespace";
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";
    public const string UrlFull = "url.full";
    public const string UrlPath = "url.path";
    public const string UrlQuery = "url.query";
    public const string HttpRoute = "http.route";
    public const string HttpRequestMethod = "http.request.method";
    public const string HttpResponseStatusCode = "http.response.status_code";
    public const string EndUserId = "enduser.id";
    public const string ExceptionType = "exception.type";
    public const string ExceptionMessage = "exception.message";
    public const string ExceptionStacktrace = "exception.stacktrace";
    public const string EksenEventId = "eksen.event.id";
    public const string EksenEventType = "eksen.event.type";
    public const string EksenHandler = "eksen.handler";
    public const string EksenSourceApp = "eksen.source_app";
    public const string EksenUowOutcome = "eksen.uow.outcome";
    public const string EksenUowTransactional = "eksen.uow.transactional";
    public const string EksenJobName = "eksen.job.name";
    public const string EksenJobResult = "eksen.job.result";
    public const string EksenOutcome = "eksen.outcome";
    public const string EksenOutboundTarget = "eksen.outbound.target";
}
