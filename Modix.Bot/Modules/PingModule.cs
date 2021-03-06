﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Modix.Services.Diagnostics;

namespace Modix.Modules
{
    [Name("Ping")]
    [Summary("Provides commands related to determining connectivity and latency.")]
    public sealed class PingModule : ModuleBase
    {
        public PingModule(
            IEnumerable<IAvailabilityEndpoint> availabilityEndpoints,
            IEnumerable<ILatencyEndpoint> latencyEndpoints)
        {
            _availabilityEndpoints = availabilityEndpoints.ToArray();
            _latencyEndpoints = latencyEndpoints.ToArray();
        }

        [Command("ping")]
        [Summary("Ping MODiX to determine connectivity and latency.")]
        public async Task PingAsync()
        {
            var message = await ReplyAsync($"Pinging {_latencyEndpoints.Count} latency endpoints " +
                $"and {_availabilityEndpoints.Count} availability endpoints...");

            var embed = new EmbedBuilder()
                .WithTitle($"Pong! Checked {_latencyEndpoints.Count + _availabilityEndpoints.Count} endpoints");

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(Timeout);

            var latencyResults = await Task.WhenAll(_latencyEndpoints
                .Select(async x => (x.DisplayName, Latency: await x.GetLatencyAsync(cancellationTokenSource.Token))));

            var availabilityResults = await Task.WhenAll(_availabilityEndpoints
                .Select(async x => (x.DisplayName, IsAvailable: await x.GetAvailabilityAsync(cancellationTokenSource.Token))));

            var average = latencyResults
                .Where(x => x.Latency.HasValue)
                .Select(x => x.Latency.Value)
                .AverageOrNull();

            embed = embed
                .WithCurrentTimestamp()
                .WithColor(LatencyColor(average));

            foreach (var result in latencyResults)
                embed.AddField(result.DisplayName, FormatLatency(result.Latency, "📶"), true);

            foreach (var result in availabilityResults)
                embed.AddField(result.DisplayName, FormatAvailability(result.IsAvailable), true);

            embed.AddField("Average", FormatLatency(average, "📈"), true);

            await message.ModifyAsync(m =>
            {
                m.Embed = embed.Build();
                m.Content = "";
            });
        }

        private string FormatLatency(
            double? latency,
            string icon)
        {
            if (latency == -1)
                return $"{icon} Error ⚠️";

            var suffix = latency switch
            {
                _ when (latency is null)    => "💔",
                _ when (latency > 300)      => "💔",
                _ when (latency > 100)      => "💛", //Yellow heart - trust me
                _ when (latency <= 100)     => "💚", //Green heart - trust me again
                _                           => "❓"
            };

            return $"{icon} {latency: 0}ms {suffix}";
        }

        private string FormatAvailability(
                bool isAvailable)
            => isAvailable ? "🌐 Up 💚" : "🌐 Down 💔";

        private Color LatencyColor(
                double? latency)
            => latency switch
            {
                _ when (latency is null)    => Color.Red,
                _ when (latency > 300)      => Color.Red,
                _ when (latency > 100)      => Color.Gold,
                _ when (latency <= 100)     => Color.Green,
                _                           => Color.Default
            };

        private const int Timeout
            = 5000;

        private readonly IReadOnlyList<IAvailabilityEndpoint> _availabilityEndpoints;
        private readonly IReadOnlyList<ILatencyEndpoint> _latencyEndpoints;
    }
}
