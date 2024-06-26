﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TextToImage;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// OpenAI text to image service.
/// </summary>
[Experimental("SKEXP0010")]
public sealed class OpenAITextToImageService : ITextToImageService
{
    private readonly OpenAITextToImageClientCore _core;

    /// <summary>
    /// OpenAI REST API endpoint
    /// </summary>
    private const string OpenAIEndpoint = "https://api.openai.com/v1/images/generations";

    /// <summary>
    /// Optional value for the OpenAI-Organization header.
    /// </summary>
    private readonly string? _organizationHeaderValue;

    /// <summary>
    /// Value for the authorization header.
    /// </summary>
    private readonly string _authorizationHeaderValue;

    /// <summary>
    /// The model to use for image generation.
    /// </summary>
    private readonly string? _modelId;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAITextToImageService"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="organization">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="modelId">The model to use for image generation.</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAITextToImageService(
        string apiKey,
        string? organization = null,
        string? modelId = null,
        HttpClient? httpClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNullOrWhiteSpace(apiKey);
        this._authorizationHeaderValue = $"Bearer {apiKey}";
        this._organizationHeaderValue = organization;
        this._modelId = modelId;

        this._core = new(httpClient, loggerFactory?.CreateLogger(this.GetType()));
        this._core.AddAttribute(OpenAIClientCore.OrganizationKey, organization);
        if (modelId is not null)
        {
            this._core.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
        }

        this._core.RequestCreated += (_, request) =>
        {
            request.Headers.Add("Authorization", this._authorizationHeaderValue);
            if (!string.IsNullOrEmpty(this._organizationHeaderValue))
            {
                request.Headers.Add("OpenAI-Organization", this._organizationHeaderValue);
            }
        };
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => this._core.Attributes;

    /// <inheritdoc/>
    public Task<string> GenerateImageAsync(string description, int width, int height, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(description);
        if (width != height || (width != 256 && width != 512 && width != 1024))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "OpenAI can generate only square images of size 256x256, 512x512, or 1024x1024.");
        }

        return this.GenerateImageAsync(this._modelId, description, width, height, "url", x => x.Url, cancellationToken);
    }

    private async Task<string> GenerateImageAsync(
        string? model,
        string description,
        int width, int height,
        string format, Func<TextToImageResponse.Image, string> extractResponse,
        CancellationToken cancellationToken)
    {
        Verify.NotNull(extractResponse);

        var requestBody = JsonSerializer.Serialize(new TextToImageRequest
        {
            Model = model,
            Prompt = description,
            Size = $"{width}x{height}",
            Count = 1,
            Format = format,
        });

        var list = await this._core.ExecuteImageGenerationRequestAsync(OpenAIEndpoint, requestBody, extractResponse!, cancellationToken).ConfigureAwait(false);
        return list[0];
    }
}
