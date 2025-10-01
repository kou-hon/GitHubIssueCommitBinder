[![.NET](https://github.com/kou-hon/GitHubIssueCommitBinder/actions/workflows/main.yml/badge.svg)](https://github.com/kou-hon/GitHubIssueCommitBinder/actions/workflows/main.yml)
[![.NET Build and Release](https://github.com/kou-hon/GitHubIssueCommitBinder/actions/workflows/BuildAndRelease.yml/badge.svg)](https://github.com/kou-hon/GitHubIssueCommitBinder/actions/workflows/BuildAndRelease.yml)

# GitHubIssueCommitBinder

GitBucketからの移管補助

## 用途

GitBucketからGitHubへ移管した場合、issueとCommitの関連が切れる  
Issueをインポートしても、Issue作成日よりもCommit日付が古いため表示されない？  
なので、インポートしたIssueに対して、関連コミットをコメントで記録しなおす

## 使い方

- ローカルリポジトリのremote originをGitHubのリポジトリとしておく
- GitHubのアクセストークンを取得しておく
- 適切なパラメータいれて実行

```
$ GitHubIssueCommitBinder.exe C:\repo\sample.git main ghp_xxx 0
```

## 実行結果

下記のようなcomment構造となる予定

| issue   | issue comment | remarks                            | 
| ------- | ------------- | :--------------------------------- | 
| issue#1 | comment1      | 元のコメント1                       | 
|         | comment2      | 元のコメント2                       | 
|         | comment3      | #1に関連したコミットのShaを列挙<br/>65cc91fdfef9e4bb9923d3ae8befbc7f21e2d4f4<br/>7591a7883faae41f057b2fecb4376c25845fc78b      | 
| issue#2 | comment1      | 元のコメント3                       | 
|         |               | 関連コミットがない場合は追加しない    | 
