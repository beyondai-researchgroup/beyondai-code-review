using System.Text;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Tests;

public class ClaudeServiceTests
{
    private static Stream ToStream(string text) =>
        new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task ParseSseStreamAsync_ContentBlockDeltaEvents_YieldsTextTokens()
    {
        var sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_01","type":"message","role":"assistant","content":[]}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" world"}}

            event: message_stop
            data: {"type":"message_stop"}

            data: [DONE]
            """;

        var tokens = new List<string>();
        await foreach (var token in ClaudeService.ParseSseStreamAsync(ToStream(sse), CancellationToken.None))
            tokens.Add(token);

        Assert.Equal(["Hello", " world"], tokens);
    }

    [Fact]
    public async Task ParseSseStreamAsync_NonDeltaLines_AreIgnored()
    {
        var sse = """
            event: ping
            data: {"type":"ping"}

            data: {"type":"message_start","message":{}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"token"}}

            data: [DONE]
            """;

        var tokens = new List<string>();
        await foreach (var token in ClaudeService.ParseSseStreamAsync(ToStream(sse), CancellationToken.None))
            tokens.Add(token);

        Assert.Single(tokens);
        Assert.Equal("token", tokens[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_DoneBeforeStreamEnd_StopsYielding()
    {
        var sse = """
            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"first"}}

            data: [DONE]

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"should not appear"}}
            """;

        var tokens = new List<string>();
        await foreach (var token in ClaudeService.ParseSseStreamAsync(ToStream(sse), CancellationToken.None))
            tokens.Add(token);

        Assert.Single(tokens);
        Assert.Equal("first", tokens[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_MalformedJsonLine_IsSkipped()
    {
        var sse = """
            data: {not valid json}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"valid"}}

            data: [DONE]
            """;

        var tokens = new List<string>();
        await foreach (var token in ClaudeService.ParseSseStreamAsync(ToStream(sse), CancellationToken.None))
            tokens.Add(token);

        Assert.Single(tokens);
        Assert.Equal("valid", tokens[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_EmptyStream_YieldsNothing()
    {
        var tokens = new List<string>();
        await foreach (var token in ClaudeService.ParseSseStreamAsync(ToStream(""), CancellationToken.None))
            tokens.Add(token);

        Assert.Empty(tokens);
    }
}
