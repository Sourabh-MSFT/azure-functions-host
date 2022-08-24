﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Az.ServerlessSecurity.Platform;
using Microsoft.Azure.WebJobs.Script.Configuration;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Configuration
{
    public class ServerlessSecurityServiceOptionsTests : IDisposable
    {
        private string _traceloggerFilename;
        private string _localFilePath;
        private string _enableSlsecAgentLog;
        private string _verifyLog;

        public ServerlessSecurityServiceOptionsTests()
        {
            //save original value to reset it to after test
            _enableSlsecAgentLog = Environment.GetEnvironmentVariable("SERVERLESS_SECURITY_LOG_CONFIG");
            _verifyLog = " message: Start up Serverless Security Agent Handler.";
            _traceloggerFilename = "\\tracelog.txt";
            _localFilePath = Directory.GetCurrentDirectory() + _traceloggerFilename;
            Environment.SetEnvironmentVariable("SERVERLESS_SECURITY_LOG_CONFIG", _localFilePath);
            File.Delete(_localFilePath);
            File.Create(_localFilePath).Dispose();
        }

        public void Dispose()
        {
            //Delete tracelogger file that was created for the test
            File.Delete(_localFilePath);
            //Reset to initial config value
            Environment.SetEnvironmentVariable("SERVERLESS_SECURITY_LOG_CONFIG", _enableSlsecAgentLog);
        }

        [Fact]
        public void ServerlessSecurityServiceOptions_ServerlessSecurityEnableSetup()
        {
            var env = new TestEnvironment();
            var token = new TestChangeTokenSource<StandbyOptions>();
            // Wire up some options.
            var host = new HostBuilder()
                .ConfigureServices(s =>
                {
                    s.AddSingleton<IEnvironment>(env);
                    s.ConfigureOptions<ServerlessSecurityDefenderOptionsSetup>();
                    s.AddSingleton<IOptionsChangeTokenSource<ServerlessSecurityDefenderOptions>, SpecializationChangeTokenSource<ServerlessSecurityDefenderOptions>>();
                    s.AddSingleton<IOptionsChangeTokenSource<StandbyOptions>>(token);
                    s.AddSingleton<ServerlessSecurityHost>();
                })
                .Build();

            // Placeholder mode
            var options = host.Services.GetService<IOptionsMonitor<ServerlessSecurityDefenderOptions>>();
            env.SetEnvironmentVariable("AZURE_FUNCTIONS_SECURITY_AGENT_ENABLED", null);
            var serverlessSecurityHost = host.Services.GetService<ServerlessSecurityHost>();
            var cancToken = new System.Threading.CancellationToken(false);
            serverlessSecurityHost.StartAsync(cancToken);
            Assert.Equal(false, options.CurrentValue.EnableDefender);
            env.SetEnvironmentVariable("AZURE_FUNCTIONS_SECURITY_AGENT_ENABLED", "1");
            // still in placeholder mode - should still have the old values.
            Assert.Equal(false, options.CurrentValue.EnableDefender);
            // Simulate specialization, which should refresh.
            token.SignalChange();
            using (StreamReader streamReader = new StreamReader(_localFilePath))
            {
                string[] b = streamReader.ReadToEnd().Split("\n");
                //check the tracelogger file for the message to verify if StartAgent was invoked
                bool isSlsecAgentEnabled = false;
                foreach (string line in b)
                {
                    string[] lineArray = line.Split(",");
                    if (lineArray.Length > 1 && lineArray[1].Equals(_verifyLog))
                    {
                        isSlsecAgentEnabled = true;
                        break;
                    }
                }
                //Assert that Agent Handler was recevied a request to StartAgent method
                Assert.Equal(true, isSlsecAgentEnabled);
            }
            //Assert that config value for enabling defender was changed to true
            Assert.Equal(true, options.CurrentValue.EnableDefender);
        }
    }
}
