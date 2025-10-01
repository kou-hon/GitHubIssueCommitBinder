using LibGit2Sharp;
using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

//sample args  C:\repo\sample.git master ghp_xxx 0
var repoPath = args.Length > 0 ? args[0] : throw new ArgumentException("Repository path is required as the first argument.");
var branchName = args.Length > 1 ? args[1] : "main";
var gitHubToken = args.Length > 2 ? args[2] : throw new ArgumentException("GitHub token is required as the third argument.");
var offsetIssueNumber = args.Length > 3 && int.TryParse(args[3], out var num) ? num : 0;

var commits = FindCommitsByMessage(repoPath, branchName);

//issue番号ごとにShaをまとめてIssueCommentを作成
var issueComments = commits
    .SelectMany(c => c.IssueNumbers.Select(num => new { num, c.Sha }))
    .GroupBy(x => x.num)
    .Select(g => new IssueComment(g.Key, g.Select(x => x.Sha).ToImmutableArray()))
    .OrderBy(c => c.issueNumber);

//リポジトリのoriginからAPIベースURLを作成
string apiBaseUrl = GetGitHubApiBaseUrl(repoPath);

//以下、GitHub APIによるコメント追記
using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", gitHubToken);

//ToDo:とりあえずすべてのコメントを一気に処理してみるが、セカンダリレートに引っかかるようだと調整する
foreach (var item in issueComments.Where(i => i.issueNumber > offsetIssueNumber))
{
    //対象issueが存在しないならエラー
    var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBaseUrl}issues/{item.issueNumber}");
    checkRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    checkRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
    var checkResponse = await client.SendAsync(checkRequest);
    if (checkResponse.IsSuccessStatusCode is false)
    {
        Console.WriteLine($"Issue #{item.issueNumber} nothing");
        Console.ReadLine();
        return;
    }

    var shaList = item.Sha.ToList();
    //すでに登録されているコミットSHAは除く
    checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{apiBaseUrl}issues/{item.issueNumber}/comments");
    checkRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    checkRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
    checkResponse = await client.SendAsync(checkRequest);
    if (checkResponse.IsSuccessStatusCode is true)
    {
        var checkContent = await checkResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(checkContent);

        //決め打ち,apiのreturnが変わったら対応必要
        var commentBodies = doc.RootElement.EnumerateArray()
            .Select(json => json.TryGetProperty("body", out var body) ? body.GetString() : null)
            .Where(s => s is not null);

        foreach (var sha in item.Sha)
        {
            if (commentBodies.Any(b => b!.Contains(sha)))
            {
                shaList.Remove(sha);
            }
        }
    }

    if(shaList.Any() is false)
    {
        Console.WriteLine($"Issue #{item.issueNumber} all sha already commented.");
        continue;
    }

    //コメントを追記
    var commentBody = new
    {
        body = "Related commits are as follows:\r\n" + string.Join(Environment.NewLine, shaList)
    };
    var commentContent = new StringContent(JsonSerializer.Serialize(commentBody));
    commentContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    var response = await client.PostAsync($"{apiBaseUrl}issues/{item.issueNumber}/comments", commentContent);
    if (response.IsSuccessStatusCode is false)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Failed to post comment to issue #{item.issueNumber}: {response.StatusCode}");
        Console.WriteLine($"Response body: {errorContent}");
        Console.ReadLine();
        return;
    }
    Console.WriteLine($"Issue #{item.issueNumber} commented. \r\n{string.Join(Environment.NewLine, shaList)}");
}

Console.WriteLine($"all comment created.");
Console.ReadLine();

IEnumerable<CommitInfo> FindCommitsByMessage(string repoPath, string targetBranchName)
{
    using (var repo = new Repository(repoPath))
    {
        var branch = repo.Branches[targetBranchName];
        if (branch is null) throw new NotFoundException();

        return repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = branch })
            .Reverse()      //古い順にする
            .Select(c => new CommitInfo(c.Sha, ExtractIssueNumbers(c.Message).ToImmutableArray(), c.Message))
            .Where(c => c.IssueNumbers.Any())
            .ToImmutableList();
    }
}
IEnumerable<int> ExtractIssueNumbers(string message)
{
    var matches = Regex.Matches(message, @"#(\d+)");
    foreach (Match match in matches)
    {
        if (int.TryParse(match.Groups[1].Value, out int num))
            yield return num;
    }
}
string GetGitHubApiBaseUrl(string repoPath)
{
    using var repo = new Repository(repoPath);
    var remote = repo.Network.Remotes["origin"];
    if (remote is null) throw new Exception("Origin remote not found.");
    var match = Regex.Match(remote.Url, @"github\.com[:/](.+?)/(.+?)(\.git)?$");
    if (!match.Success) throw new Exception("Invalid GitHub repository URL.");
    var owner = match.Groups[1].Value;
    var repoName = match.Groups[2].Value;
    return $"https://api.github.com/repos/{owner}/{repoName}/";
}

record CommitInfo(string Sha, ImmutableArray<int> IssueNumbers, string Message);

record IssueComment(int issueNumber, ImmutableArray<string> Sha);