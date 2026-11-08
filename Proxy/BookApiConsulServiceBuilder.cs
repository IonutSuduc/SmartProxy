using Consul;
using Ocelot.Discovery.Consul;
using Ocelot.Logging;

public class BookApiConsulServiceBuilder : DefaultConsulServiceBuilder
{
    public BookApiConsulServiceBuilder(
        IHttpContextAccessor contextAccessor,
        IConsulClientFactory clientFactory,
        IOcelotLoggerFactory loggerFactory)
        : base(contextAccessor, clientFactory, loggerFactory) { }

    // Folosim adresa serviciului (bookapi1/bookapi2), nu numele nodului Consul
    protected override string GetDownstreamHost(ServiceEntry entry, Node node)
        => entry.Service.Address;
}