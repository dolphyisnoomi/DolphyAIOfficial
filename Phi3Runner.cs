using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChatAppGenAI
{
    public class Phi3Runner : IDisposable
    {
        public string ModelDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "phi3");
        public bool reasoning = false;

        public Model? model;
        public Tokenizer? tokenizer;
        public TokenizerStream tokenizerStream;
        private Generator? generator;
        public GeneratorParams generatorParams;
        public bool stop = false;

        public StreamingGenerator streamingGenerator;
        public Phi3Runner something;

        public event EventHandler? ModelLoaded;

        public double temperature = 0.6;
        public int length = 2049;

        public bool thinkdeeper = false;


        [MemberNotNullWhen(true, nameof(model), nameof(tokenizer))]
        public bool IsReady => model != null && tokenizer != null;

        public bool IsInitialized => model != null && tokenizerStream != null && generatorParams != null;

        private static readonly Regex SanitizeRegex = new("[^a-zA-Z ]", RegexOptions.Compiled);

        [ThreadStatic]
        private static StringBuilder? _cachedBuilder;

        private static StringBuilder GetCachedBuilder()
        {
            _cachedBuilder ??= new StringBuilder(1024);
            _cachedBuilder.Clear();
            return _cachedBuilder;
        }
        public void Dispose()
        {
            model?.Dispose();
            tokenizerStream?.Dispose();
            tokenizer = null;
            generatorParams = null;
        }

        public IAsyncEnumerable<string> InferStreaming(
            string systemPrompt,
            string userPrompt,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var promptBuilder = new StringBuilder();

            // Optional system prompt
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                promptBuilder.AppendLine("<|im_start|>system");
                promptBuilder.AppendLine(systemPrompt.Trim());
                promptBuilder.AppendLine("<|im_end|>");
            }

            // User message
            promptBuilder.AppendLine("<|im_start|>user");
            promptBuilder.AppendLine(userPrompt.Trim());
            promptBuilder.AppendLine("<|im_end|>");

            // Assistant response begins — model will continue from here
            if(thinkdeeper is false)
            {
                promptBuilder.Append("<|im_start|>assistant\n <think></think>");
            }
            else
            {
                promptBuilder.Append("<|im_start|>assistant\n");
            }


                return InferGeneration(promptBuilder.ToString(), ct);
        }

        private void ApplySlidingWindow(int maxTokens)
        {
            if (generator.GetSequence(0).Length > maxTokens)
            {
                var trimmed = generator.GetSequence(0)[^maxTokens..];
                var trimmedText = tokenizer.Decode(trimmed);
                var encodedTrimmed = tokenizer.Encode(trimmedText);
                generator.RewindTo(1);
                generator.AppendTokenSequences(encodedTrimmed);
            }
        }

        public sealed class StreamingGenerator : IThreadPoolWorkItem
        {
            private readonly Generator generator;
            private readonly TokenizerStream tokenizerStream;
            private readonly Channel<string> channel;
            private readonly CancellationToken ct;
            private readonly Action complete;

            private const int TokenBatchLimit = 3;
            private const int EstimatedTokenLength = 4;

            private volatile bool stopRequested;

            public StreamingGenerator(
                Generator generator,
                TokenizerStream tokenizerStream,
                Channel<string> channel,
                CancellationToken ct,
                Action complete)
            {
                this.generator = generator;
                this.tokenizerStream = tokenizerStream;
                this.channel = channel;
                this.ct = ct;
                this.complete = complete;
            }

            public void RequestStop() => stopRequested = true;

            public async void Execute()
            {
                var builder = new StringBuilder(TokenBatchLimit * EstimatedTokenLength);

                try
                {
                    while (!ct.IsCancellationRequested && !generator.IsDone() && !stopRequested)
                    {
                        generator.GenerateNextToken(); // Replace with batch if supported
                        int lastToken = generator.GetSequence(0)[^1];
                        builder.Append(tokenizerStream.Decode(lastToken));

                        if (builder.Length >= TokenBatchLimit * EstimatedTokenLength)
                        {
                            await channel.Writer.WriteAsync(builder.ToString(), ct).ConfigureAwait(false);
                            builder.Clear();
                        }
                    }

                    if (builder.Length > 0)
                    {
                        await channel.Writer.WriteAsync(builder.ToString(), ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful cancellation
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Streaming error: {ex.Message}");
                }
                finally
                {
                    channel.Writer.Complete();
                    complete?.Invoke();
                }
            }
        }



        public async IAsyncEnumerable<string> InferGeneration(
      string prompt,
      [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (generator is null || tokenizer is null)
                yield break;

            if (generator.GetSequence(0).Length > 1500)
            {
                ApplySlidingWindow(1500);
            }

            // ⛓ Encode and append prompt immediately
            generator.AppendTokenSequences(tokenizer.Encode(prompt));

            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

           

            // 💥 Launch generation on pooled thread with no batching
            ThreadPool.UnsafeQueueUserWorkItem(
                streamingGenerator = new StreamingGenerator(generator, tokenizerStream, channel, ct,  () =>
                {
                    ModelLoaded?.Invoke(this, EventArgs.Empty);
                }),
                preferLocal: true
            );

            // 🎯 Yield immediately for low-latency output
            await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
            {
                yield return chunk;
            }

            ModelLoaded?.Invoke(this, EventArgs.Empty);
        }

        string BuildWarmupText()
        {
            return "<|system|>You are Dolphy AI.<|end|>" +
                   "<|user|>Hello.<|end|><|assistant|>Hi!<|end|>" +
                   "<|user|>Binary of 42?<|end|><|assistant|>101010<|end|>" +
                   "<|user|>ありがとう means?<|end|><|assistant|>Thank you<|end|>";
        }

        public async Task InitializeAsync()
        {
            var sw = Stopwatch.StartNew();
            var modelTask = Task.Run(() => new Model(ModelDir));
            await modelTask; // Await early to reduce latency

            model = modelTask.Result;

            tokenizer = new Tokenizer(model); // Direct assignment; no need to use ContinueWith

            generatorParams = new GeneratorParams(model);
            tokenizerStream = tokenizer.CreateStream();

            // Set essential generator params efficiently
            generatorParams.SetSearchOption("batch_size", 1);
            generatorParams.TryGraphCaptureWithMaxBatchSize(1);
            generatorParams.SetSearchOption("max_length", length);
            generatorParams.SetSearchOption("temperature", temperature);
            generatorParams.SetSearchOption("num_return_sequences", 1);
            generatorParams.SetSearchOption("past_present_share_buffer", true);

            var ids = tokenizer.Encode("<|end|>");
            Debug.WriteLine($"Token count: {ids.NumSequences}");
            Debug.WriteLine($"Token IDs: {string.Join(", ", ids)}");

            try
            {
                generator = new Generator(model, generatorParams);

                var warmupText = BuildWarmupText();
                generator.AppendTokenSequences(tokenizer.Encode(warmupText));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Warm-up failed: {ex.Message}");
            }

            sw.Stop();
            Debug.WriteLine($"Model initialized in {sw.ElapsedMilliseconds} ms");
            ModelLoaded?.Invoke(this, EventArgs.Empty);
        }

    }



        public class PhiMessage
    {
        public string Text { get; set; }
        public PhiMessageType Type { get; set; }

        public PhiMessage(string text, PhiMessageType type)
        {
            Text = text;
            Type = type;
        }
    }

    public enum PhiMessageType
    {
        User,
        Assistant
    }
}