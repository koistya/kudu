﻿using System;
using System.Web;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class GitlabHqHandler : GitHubCompatHandler
    {
        protected override GitDeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            var repository = payload.Value<JObject>("repository");
            var userid = payload.Value<int?>("user_id");
            var username = payload.Value<string>("user_name");

            if (repository == null || userid == null || username == null)
            {
                // doesn't look like GitlabHQ
                return null;
            }

            string newRef = payload.Value<string>("after");
            if (IsDeleteCommit(newRef))
            {
                return null;
            }
            var commits = payload.Value<JArray>("commits");
            var info = new GitDeploymentInfo { RepositoryType = RepositoryType.Git };
            info.NewRef = payload.Value<string>("after");
            info.TargetChangeset = ParseChangeSet(info.NewRef, commits);

            // gitlabHq format
            // { "before":"34d62c0ad9387a8b9274ad77e878e195c342772b", "after":"02652ef69da7ee3d49134a961bffcb50702661ce", "ref":"refs/heads/master", "user_id":1, "user_name":"Remco Ros", "repository":{ "name":"inspectbin", "url":"http://gitlab.proscat.nl/inspectbin", "description":null, "homepage":"http://gitlab.proscat.nl/inspectbin"  }, "commits":[ { "id":"4109312962bb269ecc3a0d7a3c82a119dcd54c8b", "message":"add uservoice", "timestamp":"2012-11-11T14:32:02+01:00", "url":"http://gitlab.proscat.nl/inspectbin/commits/4109312962bb269ecc3a0d7a3c82a119dcd54c8b", "author":{ "name":"Remco Ros", "email":"r.ros@proscat.nl" }}], "total_commits_count":12 }
            info.RepositoryUrl = repository.Value<string>("url");
            

            // Currently Gitlab url's are broken.
            if (!info.RepositoryUrl.EndsWith(".git", StringComparison.Ordinal))
            {
                info.RepositoryUrl += ".git";
            }

            // work around missing 'private' property, if missing assume is private.
            JToken priv;
            bool isPrivate = true;
            if (repository.TryGetValue("private", out priv))
            {
                isPrivate = priv.ToObject<bool>();
            }

            // The format of ref is refs/something/something else
            // For master it's normally refs/head/master
            string @ref = payload.Value<string>("ref");

            if (String.IsNullOrEmpty(@ref))
            {
                return null;
            }
            info.Deployer = "GitlabHQ";

            // private repo, use SSH
            if (isPrivate)
            {
                Uri uri = new Uri(info.RepositoryUrl);
                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var host = "git@" + uri.Host;
                    info.RepositoryUrl = host + ":" + uri.AbsolutePath.TrimStart('/');
                }
            }

            return info;
        }
    }
}