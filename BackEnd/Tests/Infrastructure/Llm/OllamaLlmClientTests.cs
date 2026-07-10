using System.Net;
using System.Text;
using System.Text.Json;
using Application.DTOs.Llm;
using Application.Exceptions;
using FluentAssertions;
using Infrastructure.Configuration;
using Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Llm;

public sealed class OllamaLlmClientTests
{
    [Fact]
    public async Task CompleteChatAsync_maps_request_payload_to_ollama_format()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return SuccessResponse(
                """
                {
                  "model": "custom-model",
                  "message": { "role": "assistant", "content": "ok" }
                }
                """);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 0
        });

        var chatRequest = new LlmChatRequest(
            [
                new LlmMessage(LlmMessageRole.System, "You are helpful."),
                new LlmMessage(LlmMessageRole.User, "Hello")
            ],
            Model: "custom-model",
            Temperature: 0.3,
            MaxOutputTokens: 256);

        // Act
        await client.CompleteChatAsync(chatRequest);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be("http://localhost:11434/api/chat");

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be("custom-model");
        root.GetProperty("stream").GetBoolean().Should().BeFalse();

        var messages = root.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("You are helpful.");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("Hello");

        root.GetProperty("options").GetProperty("temperature").GetDouble().Should().Be(0.3);
        root.GetProperty("options").GetProperty("num_predict").GetInt32().Should().Be(256);
    }

    [Fact]
    public async Task CompleteChatAsync_returns_successful_response()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => SuccessResponse(
            """
            {
              "model": "llama3.2",
              "message": { "role": "assistant", "content": "hello from ollama" },
              "prompt_eval_count": 10,
              "eval_count": 5
            }
            """));

        var client = CreateClient(handler);
        var chatRequest = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);

        // Act
        var response = await client.CompleteChatAsync(chatRequest);

        // Assert
        response.Content.Should().Be("hello from ollama");
        response.Model.Should().Be("llama3.2");
        response.Usage.Should().NotBeNull();
        response.Usage!.InputTokens.Should().Be(10);
        response.Usage.OutputTokens.Should().Be(5);
        response.Usage.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task CompleteChatAsync_retries_on_transient_503()
    {
        // Arrange
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;

            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return SuccessResponse(
                """
                {
                  "model": "llama3.2",
                  "message": { "role": "assistant", "content": "recovered" }
                }
                """);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        });

        var chatRequest = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);

        // Act
        var response = await client.CompleteChatAsync(chatRequest);

        // Assert
        attempts.Should().Be(2);
        response.Content.Should().Be("recovered");
    }

    [Fact]
    public async Task CompleteChatWithToolsAsync_maps_request_payload_with_tools()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return SuccessResponse(
                """
                {
                  "model": "llama3.2",
                  "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                      {
                        "function": {
                          "name": "create_task",
                          "arguments": {
                            "title": "Buy milk",
                            "dueDate": "2026-07-10"
                          }
                        }
                      }
                    ]
                  }
                }
                """);
        });

        var client = CreateClient(handler);
        var chatRequest = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "Create a task")]);
        var tools =
            new List<LlmToolDefinition>
            {
                new(
                    "create_task",
                    "Creates a task.",
                    """
                    {
                      "type": "object",
                      "properties": {
                        "title": { "type": "string" },
                        "dueDate": { "type": "string" }
                      },
                      "required": ["title", "dueDate"]
                    }
                    """)
            };

        // Act
        var response = await client.CompleteChatWithToolsAsync(chatRequest, tools);

        // Assert
        capturedRequest.Should().NotBeNull();
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.TryGetProperty("tools", out var toolsElement).Should().BeTrue();
        toolsElement.GetArrayLength().Should().Be(1);
        toolsElement[0].GetProperty("type").GetString().Should().Be("function");
        toolsElement[0].GetProperty("function").GetProperty("name").GetString().Should().Be("create_task");

        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be("create_task");
        response.ToolCalls[0].Id.Should().Be("call_0_create_task");
        response.ToolCalls[0].Arguments.Should().Contain("Buy milk");
    }

    [Fact]
    public async Task CompleteChatWithToolsAsync_retries_on_transient_503()
    {
        // Arrange
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;

            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return SuccessResponse(
                """
                {
                  "model": "llama3.2",
                  "message": { "role": "assistant", "content": "done" }
                }
                """);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        });

        var chatRequest = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);
        var tools = new List<LlmToolDefinition>
        {
            new("create_task", "Creates a task.", """{"type":"object","properties":{}}""")
        };

        // Act
        var response = await client.CompleteChatWithToolsAsync(chatRequest, tools);

        // Assert
        attempts.Should().Be(2);
        response.Content.Should().Be("done");
    }

    [Fact]
    public async Task CompleteChatAsync_throws_non_transient_exception_on_404()
    {
        // Arrange
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            Model = "llama3.2",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        });

        var chatRequest = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);

        // Act
        var act = () => client.CompleteChatAsync(chatRequest);

        // Assert
        var exception = await act.Should().ThrowAsync<LlmException>();
        exception.Which.IsTransient.Should().BeFalse();
        attempts.Should().Be(1);
    }

    private static OllamaLlmClient CreateClient(
        HttpMessageHandler handler,
        LlmSettings? settings = null) =>
        new(
            new HttpClient(handler, disposeHandler: true),
            Options.Create(settings ?? new LlmSettings
            {
                Provider = LlmSettings.OllamaProvider,
                Model = "llama3.2",
                BaseUrl = "http://localhost:11434",
                TimeoutSeconds = 60,
                MaxRetryAttempts = 0
            }),
            NullLogger<OllamaLlmClient>.Instance);

    private static HttpResponseMessage SuccessResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
