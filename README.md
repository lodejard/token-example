# token-example

Examples of k8s service-to-service rbac in dotnet.

Adding caller's bearer token to request
```cs
using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

var httpRequest = new HttpRequestMessage(HttpMethod.Get, serverAppUrl);
await kubernetes.Credentials.ProcessHttpRequestAsync(httpRequest, default);
```

Doing the same thing, but with a message handler.
```cs
using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

using var httpClient = new HttpClient(
    new CredentialsHandler(kubernetes.Credentials)
    {
        InnerHandler = new HttpClientHandler()
    });

var result = await httpClient.GetStringAsync(serverAppUrl);

// ....

public class CredentialsHandler : DelegatingHandler
{
    private ServiceClientCredentials credentials;

    public CredentialsHandler(ServiceClientCredentials credentials)
    {
        this.credentials = credentials;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await this.credentials.ProcessHttpRequestAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

Authenticating bearer token on the receiving end.
```cs
if (AuthenticationHeaderValue.TryParse(httpContext.Request.Headers["Authorization"], out var auth) &&
    string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
{
    using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

    var tokenReview = await kubernetes.CreateTokenReviewAsync(
        new V1TokenReview(
            new V1TokenReviewSpec(token: auth.Parameter)));
}
```
