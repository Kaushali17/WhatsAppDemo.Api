using System.Net;
using System.Text;

namespace WhatsAppDemo.Api.Tests.TestInfrastructure
{
    /// <summary>
    /// Intercepts every request made through WhatsAppService's Meta HttpClient so tests
    /// never call the real WhatsApp Cloud API. Responses are enqueued FIFO per test.
    /// </summary>
    public class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Body)> _responses = new();

        public Uri? LastRequestUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastAuthorizationHeader { get; private set; }
        public string? LastRequestBody { get; private set; }
        public int RequestCount { get; private set; }

        public void EnqueueResponse(HttpStatusCode statusCode, string jsonBody)
            => _responses.Enqueue((statusCode, jsonBody));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastAuthorizationHeader = request.Headers.Authorization?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "StubHttpMessageHandler received a request but no canned response was enqueued.");
            }

            var (statusCode, body) = _responses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
