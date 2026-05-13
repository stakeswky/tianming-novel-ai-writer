using System;
using TM.Framework.Platform;
using Xunit;

namespace Tianming.Framework.Tests.Platform;

public class ScutilProxyOutputParserTests
{
    [Fact]
    public void Parse_EmptyOutput_ReturnsDirect()
    {
        var result = ScutilProxyOutputParser.Parse(string.Empty);
        Assert.Same(ProxyPolicy.Direct, result);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsDirect()
    {
        var result = ScutilProxyOutputParser.Parse("   \n\n   ");
        Assert.Same(ProxyPolicy.Direct, result);
    }

    [Fact]
    public void Parse_ProxyOff_ReturnsDirect_WithExceptions()
    {
        var output = """
            <dictionary> {
              ExceptionsList : <array> {
                0 : *.local
                1 : 169.254/16
              }
              HTTPEnable : 0
              HTTPSEnable : 0
              SOCKSEnable : 0
            }
            """;

        var result = ScutilProxyOutputParser.Parse(output);

        Assert.False(result.HasProxy);
        Assert.Contains("*.local", result.Exceptions);
        Assert.Contains("169.254/16", result.Exceptions);
    }

    [Fact]
    public void Parse_HttpProxyOn_ExtractsHttpUri_HttpsNull()
    {
        var output = """
            <dictionary> {
              ExceptionsList : <array> { 0 : localhost }
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
              HTTPSEnable : 0
            }
            """;

        var result = ScutilProxyOutputParser.Parse(output);

        Assert.Equal(new Uri("http://127.0.0.1:7890"), result.HttpProxy);
        Assert.Null(result.HttpsProxy);
        Assert.True(result.HasProxy);
    }

    [Fact]
    public void Parse_BothHttpAndHttps_ExtractsBoth()
    {
        var output = """
            <dictionary> {
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
              HTTPSEnable : 1
              HTTPSPort : 7890
              HTTPSProxy : 127.0.0.1
            }
            """;

        var result = ScutilProxyOutputParser.Parse(output);

        Assert.Equal(new Uri("http://127.0.0.1:7890"), result.HttpProxy);
        Assert.Equal(new Uri("http://127.0.0.1:7890"), result.HttpsProxy);
    }

    [Fact]
    public void Parse_EnabledButMissingProxyField_TreatedAsDirect()
    {
        var output = """
            <dictionary> {
              HTTPEnable : 1
              HTTPSEnable : 0
            }
            """;

        var result = ScutilProxyOutputParser.Parse(output);

        Assert.False(result.HasProxy);
        Assert.Null(result.HttpProxy);
    }

    [Fact]
    public void Parse_RealWorldSnapshot_TailscaleStyle()
    {
        // 来自一台开了 Surge / Clash 转发 + Tailscale 的 macOS 机器
        var output = """
            <dictionary> {
              ExceptionsList : <array> {
                0 : 100.100.100.100.dns
                1 : 198.18.0.0/15
                2 : captive.apple.com
                3 : localhost
                4 : 10.0.0.0/8
                5 : *.local
                6 : 172.16.0.0/12
                7 : tailscale.io
                8 : tailscale.com
              }
              ExcludeSimpleHostnames : 1
              FTPPassive : 1
              HTTPEnable : 1
              HTTPPort : 1082
              HTTPProxy : 127.0.0.1
              HTTPSEnable : 1
              HTTPSPort : 1082
              HTTPSProxy : 127.0.0.1
              ProxyAutoConfigEnable : 0
              ProxyAutoDiscoveryEnable : 0
              SOCKSEnable : 0
            }
            """;

        var result = ScutilProxyOutputParser.Parse(output);

        Assert.Equal(new Uri("http://127.0.0.1:1082"), result.HttpProxy);
        Assert.Equal(new Uri("http://127.0.0.1:1082"), result.HttpsProxy);
        Assert.Contains("localhost", result.Exceptions);
        Assert.Contains("*.local", result.Exceptions);
        Assert.Contains("tailscale.com", result.Exceptions);
    }
}
