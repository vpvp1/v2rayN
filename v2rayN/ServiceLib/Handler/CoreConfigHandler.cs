namespace ServiceLib.Handler;

/// <summary>
/// Core configuration file processing class
/// </summary>
public static class CoreConfigHandler
{
    private static readonly string _tag = "CoreConfigHandler";

    public static async Task<RetResult> GenerateClientConfig(CoreConfigContext context, string? fileName)
    {
        var config = AppManager.Instance.Config;
        var result = new RetResult();
        var node = context.Node;

        if (node.ConfigType == EConfigType.Custom)
        {
            result = node.CoreType switch
            {
                ECoreType.mihomo => await new CoreConfigClashService(config).GenerateClientCustomConfig(node, fileName),
                _ => await GenerateClientCustomConfig(node, fileName)
            };
        }
        else if (context.RunCoreType == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        }
        else
        {
            result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        }
        if (result.Success != true)
        {
            return result;
        }
        if (fileName.IsNotEmpty() && result.Data != null)
        {
            await File.WriteAllTextAsync(fileName, result.Data.ToString());
        }

        return result;
    }

    private static async Task<RetResult> GenerateClientCustomConfig(ProfileItem node, string? fileName)
    {
        var ret = new RetResult();
        try
        {
            if (node == null || fileName is null)
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            if (File.Exists(fileName))
            {
                File.SetAttributes(fileName, FileAttributes.Normal); //If the file has a read-only attribute, direct deletion will fail
                File.Delete(fileName);
            }

            var addressFileName = node.Address;
            if (!File.Exists(addressFileName))
            {
                addressFileName = Utils.GetConfigPath(addressFileName);
            }
            if (!File.Exists(addressFileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }
            // Outbound Interface patch: inject sendThrough into custom config
            var customContent = await File.ReadAllTextAsync(addressFileName);
            var outboundInterface = AppManager.Instance.Config.CoreBasicItem.OutboundInterface?.Trim();
            if (!string.IsNullOrEmpty(outboundInterface))
            {
                customContent = InjectSendThroughIntoJson(customContent, outboundInterface);
            }
            await File.WriteAllTextAsync(fileName, customContent);
            File.SetAttributes(fileName, FileAttributes.Normal);

            //check again
            if (!File.Exists(fileName))
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            return await Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    private static string InjectSendThroughIntoJson(string json, string interfaceIP)
    {
        try
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject();
            if (root == null) return json;

            // Patch outbounds
            if (root["outbounds"] is System.Text.Json.Nodes.JsonArray outbounds)
            {
                foreach (var item in outbounds)
                {
                    var ob = item?.AsObject();
                    if (ob == null) continue;
                    var proto = ob["protocol"]?.GetValue<string>() ?? string.Empty;
                    if (proto is "freedom" or "blackhole" or "dns" or "loopback")
                        continue;
                    ob.Remove("sendThrough");
                    ob.Add("sendThrough", System.Text.Json.Nodes.JsonValue.Create(interfaceIP));
                }
            }

            // Patch DNS servers
            if (root["dns"]?.AsObject() is System.Text.Json.Nodes.JsonObject dnsObj &&
                dnsObj["servers"] is System.Text.Json.Nodes.JsonArray dnsServers)
            {
                var patched = new System.Text.Json.Nodes.JsonArray();
                foreach (var entry in dnsServers)
                {
                    if (entry is System.Text.Json.Nodes.JsonValue strVal &&
                        strVal.TryGetValue<string>(out var addr))
                    {
                        patched.Add(new System.Text.Json.Nodes.JsonObject
                        {
                            ["address"]     = addr,
                            ["sendThrough"] = interfaceIP
                        });
                    }
                    else if (entry?.AsObject() is System.Text.Json.Nodes.JsonObject obj)
                    {
                        obj.Remove("sendThrough");
                        obj.Add("sendThrough", System.Text.Json.Nodes.JsonValue.Create(interfaceIP));
                        patched.Add(System.Text.Json.Nodes.JsonNode.Parse(obj.ToJsonString()));
                    }
                    else
                    {
                        patched.Add(entry?.DeepClone());
                    }
                }
                dnsObj.Remove("servers");
                dnsObj.Add("servers", patched);
            }

            return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, string fileName, List<ServerTestItem> selecteds, ECoreType coreType)
    {
        var result = new RetResult();
        var dummyNode = new ProfileItem
        {
            CoreType = coreType
        };
        var builderResult = await CoreConfigContextBuilder.Build(config, dummyNode);
        var context = builderResult.Context;
        foreach (var testItem in selecteds)
        {
            var node = testItem.Profile;
            var (actNode, _) = await CoreConfigContextBuilder.ResolveNodeAsync(context, node, true);
            if (node.IndexId == actNode.IndexId)
            {
                continue;
            }
            context.ServerTestItemMap[node.IndexId] = actNode.IndexId;
        }
        if (coreType == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientSpeedtestConfig(selecteds);
        }
        else if (coreType == ECoreType.Xray)
        {
            result = new CoreConfigV2rayService(context).GenerateClientSpeedtestConfig(selecteds);
        }
        if (result.Success != true)
        {
            return result;
        }
        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }

    public static async Task<RetResult> GenerateClientSpeedtestConfig(Config config, CoreConfigContext context, ServerTestItem testItem, string fileName)
    {
        var result = new RetResult();
        var initPort = AppManager.Instance.GetLocalPort(EInboundProtocol.speedtest);
        var port = Utils.GetFreePort(initPort + testItem.QueueNum);
        testItem.Port = port;

        if (context.RunCoreType == ECoreType.sing_box)
        {
            result = new CoreConfigSingboxService(context).GenerateClientSpeedtestConfig(port);
        }
        else
        {
            result = new CoreConfigV2rayService(context).GenerateClientSpeedtestConfig(port);
        }
        if (result.Success != true)
        {
            return result;
        }

        await File.WriteAllTextAsync(fileName, result.Data.ToString());
        return result;
    }
}
