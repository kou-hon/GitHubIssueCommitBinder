using LibGit2Sharp;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

//sampleargs  C:\repo\sample.git master
var repoPath = args.Length > 0 ? args[0] : throw new ArgumentException("Repository path is required as the first argument.");
var branchName = args.Length > 1 ? args[1] : "main";

var commits = FindCommitsByMessage(repoPath, branchName);

//issue番号ごとにShaをまとめてIssueCommentを作成する
var issueComments = commits
    .SelectMany(c => c.IssueNumbers.Select(num => new { num, c.Sha }))
    .GroupBy(x => x.num)
    .Select(g => new IssueComment(g.Key, g.Select(x => x.Sha).ToImmutableArray()))
    .OrderBy(c => c.issueNumber);

var json = JsonSerializer.Serialize(issueComments.ToArray(), new JsonSerializerOptions { WriteIndented = true });
var outputPath = Path.Combine(Path.GetDirectoryName(repoPath)!, $"comments_{Path.GetFileNameWithoutExtension(repoPath)}.json");
File.WriteAllText(outputPath, json);

Console.WriteLine($"{outputPath} created.");
Console.ReadLine();

IEnumerable<CommitInfo> FindCommitsByMessage(string repoPath, string targetBranchName)
{
    using (var repo = new Repository(repoPath))
    {
        var branch = repo.Branches[targetBranchName];
        if (branch is null) throw new NotFoundException();

        var commits = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = branch })
            .Reverse()      //古い順にする
            .Select(c => new CommitInfo(c.Sha, ExtractIssueNumbers(c.Message).ToImmutableArray(), c.Message));
        foreach (var info in commits.Where(c => c.IssueNumbers.Any()))
        {
            yield return info;
        }
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
record CommitInfo(string Sha, ImmutableArray<int> IssueNumbers, string Message);

record IssueComment(int issueNumber, ImmutableArray<string> Sha);
