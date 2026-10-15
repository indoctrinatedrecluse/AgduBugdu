using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgduBugdu.PluginContracts;

namespace AgduBugdu.Plugin.RestClient;

public class RestClientExtension : IExtension
{
    private IExtensionContext? _context;
    private RestClientViewModel? _viewModel;

    public string Id => "agdubugdu.plugin.restclient";
    public string Name => "REST Client & API Runner";
    public string Version => "1.0.0";

    public RestClientViewModel? ViewModel => _viewModel;

    public void Initialize(IExtensionContext context)
    {
        _context = context;
        _viewModel = new RestClientViewModel();

        // 1. Register commands
        _context.Commands.RegisterCommand(
            "rest.send",
            "REST Client: Send Active Request",
            async () =>
            {
                if (_viewModel != null)
                {
                    // Check if active document is .http or .rest
                    string? activePath = _context.EditorService.ActiveDocumentPath;
                    string? docText = _context.EditorService.GetActiveDocumentText();

                    if (!string.IsNullOrEmpty(docText) &&
                        (activePath?.EndsWith(".http", StringComparison.OrdinalIgnoreCase) == true ||
                         activePath?.EndsWith(".rest", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        var reqs = HttpFileParser.ParseHttpFile(docText);
                        if (reqs.Count > 0)
                        {
                            var req = reqs[0];
                            _viewModel.SetRequest(req);
                            await _viewModel.SendRequestAsync(req);
                            _context.Log($"[REST] Executed {req.Method} {req.Url} -> {_viewModel.LastResponse?.StatusCode} ({_viewModel.LastResponse?.ElapsedMilliseconds} ms)");
                            return;
                        }
                    }

                    // Otherwise execute current workbench request
                    await _viewModel.SendRequestAsync();
                    _context.Log($"[REST] Executed {_viewModel.Method} {_viewModel.Url} -> {_viewModel.LastResponse?.StatusCode} ({_viewModel.LastResponse?.ElapsedMilliseconds} ms)");
                }
            },
            "Ctrl+Alt+R"
        );

        _context.Commands.RegisterCommand(
            "rest.new",
            "REST Client: New .http Request File",
            () =>
            {
                string template = @"### Sample API Request
GET https://httpbin.org/get
Accept: application/json

### POST JSON Payload
POST https://httpbin.org/post
Content-Type: application/json

{
  ""name"": ""AgduBugdu"",
  ""type"": ""Developer IDE"",
  ""version"": ""1.5.1""
}
";
                _context.EditorService.NewDocument("requests.http", template);
                return Task.CompletedTask;
            }
        );

        // 2. Register Tool Window
        _context.ToolWindows.RegisterToolWindow(
            "rest.client.tool",
            "REST Client",
            () => _viewModel,
            vm => _viewModel,
            ToolDockPosition.Bottom
        );

        // 3. Register Language & Snippets
        _context.Languages.RegisterLanguage(new LanguageDefinition(
            "http",
            "HTTP / REST Request",
            new[] { ".http", ".rest" },
            "source.http",
            "#",
            null,
            null
        ));

        _context.Languages.RegisterSnippet(new Snippet(
            "http.get",
            "GET Request",
            "get",
            "Standard HTTP GET Request block",
            "### ${Name}\nGET ${https://api.example.com/endpoint}\nAccept: application/json\n\n$0",
            "http"
        ));

        _context.Languages.RegisterSnippet(new Snippet(
            "http.post",
            "POST Request",
            "post",
            "HTTP POST Request with JSON Body",
            "### ${Name}\nPOST ${https://api.example.com/endpoint}\nContent-Type: application/json\n\n{\n    \"${key}\": \"${value}\"$0\n}",
            "http"
        ));

        _context.Log("REST Client extension initialized.");
    }

    public Task ActivateAsync() => Task.CompletedTask;
    public Task DeactivateAsync() => Task.CompletedTask;
}
