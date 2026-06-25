using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using PersonalTaskManager.App.Infrastructure;

namespace PersonalTaskManager.App;

public sealed class MattyBodyNode
{
    public bool IsImage { get; init; }

    public string Text { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;
}

public sealed class MattyImportResult
{
    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public IReadOnlyList<MattyBodyNode> BodyNodes { get; init; } = [];

    public IReadOnlyList<string> Comments { get; init; } = [];
}

public partial class MattyImportWindow : Window
{
    private const string BaseUrl = "https://easymedia.matty.works:8443";

    private static MattyImportWindow? shared;

    private TaskCompletionSource<MattyImportResult?>? completion;
    private string taskId = string.Empty;
    private bool initialized;
    private bool busy;
    private bool settled;

    public MattyImportWindow()
    {
        InitializeComponent();
    }

    // 세션 유지를 위해 단일 인스턴스를 재사용한다(로그인 1회).
    public static async Task<MattyImportResult?> FetchAsync(Window owner, string taskId)
    {
        shared ??= new MattyImportWindow();
        shared.Owner = owner;
        return await shared.Fetch(taskId);
    }

    private async Task<MattyImportResult?> Fetch(string id)
    {
        taskId = id;
        completion = new TaskCompletionSource<MattyImportResult?>();
        settled = false;
        StatusText.Text = "메티에 접속 중...";

        Show();
        Activate();

        if (!await EnsureBrowser())
        {
            return await Finish(null);
        }

        Web.CoreWebView2.Navigate($"{BaseUrl}/Task/Go/{taskId}");
        return await completion.Task;
    }

    private async Task<bool> EnsureBrowser()
    {
        if (initialized)
        {
            return true;
        }

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, AppDataPaths.WebView2Folder);
            await Web.EnsureCoreWebView2Async(env);
            Web.CoreWebView2.NavigationCompleted += async (_, _) => await TryFetch(auto: true);
            initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"브라우저 초기화 실패: {ex.Message}";
            return false;
        }
    }

    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        await TryFetch(auto: false);
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        await Finish(null);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 실제로 닫지 않고 숨겨 인스턴스(세션)를 유지한다.
        e.Cancel = true;
        if (completion is { Task.IsCompleted: false })
        {
            completion.TrySetResult(null);
        }

        Hide();
    }

    private async Task<MattyImportResult?> Finish(MattyImportResult? result)
    {
        settled = true;
        Hide();
        completion?.TrySetResult(result);
        await Task.CompletedTask;
        return result;
    }

    private async Task TryFetch(bool auto)
    {
        if (busy || settled || Web.CoreWebView2 is null)
        {
            return;
        }

        busy = true;
        try
        {
            FetchButton.IsEnabled = false;
            var clicked = await Web.CoreWebView2.ExecuteScriptAsync(BuildClickScript(taskId));
            if (!string.Equals(clicked, "true", StringComparison.OrdinalIgnoreCase))
            {
                if (!auto)
                {
                    StatusText.Text = "테스크를 찾지 못했습니다. 메티 로그인 후 다시 '가져오기'를 누르세요.";
                }

                return;
            }

            StatusText.Text = "테스크 상세를 불러오는 중...";

            MattyImportResult? best = null;
            var stable = 0;
            for (var i = 0; i < 50 && !settled; i++)
            {
                var raw = await Web.CoreWebView2.ExecuteScriptAsync(ExtractScript);
                if (TryParse(raw, out var current))
                {
                    if (best is null || current.Comments.Count > best.Comments.Count)
                    {
                        best = current;
                        stable = 0;
                    }
                    else
                    {
                        stable++;
                    }

                    // 본문 확보 후 댓글 수가 안정되면 종료(댓글 AJAX 로딩 대기).
                    if (best is not null && stable >= 4)
                    {
                        break;
                    }
                }

                await Task.Delay(300);
            }

            if (best is not null)
            {
                await Finish(best);
                return;
            }

            StatusText.Text = "테스크 상세를 불러오지 못했습니다. 다시 시도해 주세요.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"가져오기 실패: {ex.Message}";
        }
        finally
        {
            FetchButton.IsEnabled = true;
            busy = false;
        }
    }

    public static Task<Dictionary<string, byte[]>> DownloadBodyImagesAsync(IReadOnlyCollection<string> urls)
    {
        return shared is null ? Task.FromResult(new Dictionary<string, byte[]>()) : shared.DownloadImagesAsync(urls);
    }

    // 메티 인증 쿠키로 본문 이미지를 내려받는다.
    public async Task<Dictionary<string, byte[]>> DownloadImagesAsync(IReadOnlyCollection<string> urls)
    {
        var map = new Dictionary<string, byte[]>();
        if (urls.Count == 0 || Web.CoreWebView2 is null)
        {
            return map;
        }

        var cookies = await Web.CoreWebView2.CookieManager.GetCookiesAsync(BaseUrl);
        var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

        using var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);

        foreach (var url in urls.Distinct())
        {
            try
            {
                map[url] = await http.GetByteArrayAsync(url);
            }
            catch
            {
                // 개별 이미지 실패는 무시하고 나머지를 계속 받는다.
            }
        }

        return map;
    }

    private static bool TryParse(string raw, out MattyImportResult result)
    {
        result = new MattyImportResult();
        if (string.IsNullOrEmpty(raw) || raw == "null")
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            {
                return false;
            }

            var comments = new List<string>();
            if (root.TryGetProperty("comments", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        comments.Add(text.Trim());
                    }
                }
            }

            var nodes = new List<MattyBodyNode>();
            if (root.TryGetProperty("bodyNodes", out var bn) && bn.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bn.EnumerateArray())
                {
                    var type = item.TryGetProperty("t", out var tp) ? tp.GetString() : null;
                    var value = item.TryGetProperty("v", out var vp) ? vp.GetString() ?? string.Empty : string.Empty;
                    if (type == "img" && !string.IsNullOrWhiteSpace(value))
                    {
                        nodes.Add(new MattyBodyNode { IsImage = true, ImageUrl = value });
                    }
                    else if (type == "text" && value.Length > 0)
                    {
                        nodes.Add(new MattyBodyNode { IsImage = false, Text = value });
                    }
                }
            }

            result = new MattyImportResult
            {
                Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Body = root.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty,
                BodyNodes = nodes,
                Comments = comments
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildClickScript(string id)
    {
        return @"(function(){
  var id='__ID__';
  var li=Array.prototype.slice.call(document.querySelectorAll('li[data-id]')).find(function(e){return e.getAttribute('data-id')===id;});
  if(li){var a=li.querySelector('a'); if(a){a.click(); return true;}}
  return false;
})()".Replace("__ID__", id);
    }

    private const string ExtractScript = @"(function(){
  var m=document.querySelector('#taskdetail');
  if(!(m && m.classList.contains('in'))) return {ok:false};
  var bodyEl=m.querySelector('.modal-body.yellow-gold [data-type=""task""]') || m.querySelector('.modal-body.yellow-gold');
  if(!bodyEl) return {ok:false};
  function nodes(el){
    var segs=[];
    function walk(node){
      for(var i=0;i<node.childNodes.length;i++){
        var n=node.childNodes[i];
        if(n.nodeType===3){ var tx=n.textContent; if(tx) segs.push({t:'text',v:tx}); }
        else if(n.nodeType===1){
          var tag=n.tagName;
          if(tag==='IMG'){ var s=n.src||''; if(s && !/avatar|noimage|\/Profile\/|emoticon/i.test(s)) segs.push({t:'img',v:s}); }
          else if(tag==='BR'){ segs.push({t:'text',v:'\n'}); }
          else { var disp=(tag==='DIV'||tag==='P'||tag==='LI'); if(disp) segs.push({t:'text',v:'\n'}); walk(n); }
        }
      }
    }
    walk(el);
    return segs;
  }
  var title=(m.querySelector('.modal-title')||{}).innerText||'';
  var body=bodyEl.innerText||'';
  var bodyNodes=nodes(bodyEl);
  var cbox=m.querySelector('.modal-body.taskcomments');
  var comments=[];
  if(cbox){
    comments=Array.prototype.slice.call(cbox.querySelectorAll('.cmt_body')).map(function(c){return (c.innerText||'').trim();}).filter(function(t){return t.length>0;});
  }
  return {ok:true, title:title.trim(), body:body.trim(), bodyNodes:bodyNodes, comments:comments};
})()";
}
