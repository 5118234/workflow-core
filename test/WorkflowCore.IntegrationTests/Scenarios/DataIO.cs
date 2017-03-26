﻿using Machine.Fakes;
using Machine.Fakes.Adapters.Moq;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Services;

namespace WorkflowCore.IntegrationTests.Scenarios
{
    class DataIODataClass
    {
        public int Value1 { get; set; }
        public int Value2 { get; set; }
        public int Value3 { get; set; }
    }

    [Behaviors]
    public class DataIOBehavior
    {
        static string WorkflowId;
        static IPersistenceProvider PersistenceProvider;
        static WorkflowInstance Instance;

        It should_be_marked_as_complete = () => Instance.Status.ShouldEqual(WorkflowStatus.Complete);
        It should_have_a_return_value_of_5 = () => (Instance.Data as DataIODataClass).Value3.ShouldEqual(5);
    }

    [Subject(typeof(WorkflowHost))]
    public class DataIO : WithFakes<MoqFakeEngine>
    {
        class AddNumbers : StepBody
        {
            public int Input1 { get; set; }
            public int Input2 { get; set; }
            public int Output { get; set; }

            public override ExecutionResult Run(IStepExecutionContext context)
            {
                Output = (Input1 + Input2);
                return ExecutionResult.Next();
            }
        }
                
        class DataIOWorkflow : IWorkflow<DataIODataClass>
        {
            public string Id { get { return "DataIOWorkflow"; } }
            public int Version { get { return 1; } }
            public void Build(IWorkflowBuilder<DataIODataClass> builder)
            {
                builder
                    .StartWith<AddNumbers>()
                        .Input(step => step.Input1, data => data.Value1)
                        .Input(step => step.Input2, data => data.Value2)
                        .Output(data => data.Value3, step => step.Output);
            }
        }
                        
        static IWorkflowHost Host;
        static string WorkflowId;
        static IPersistenceProvider PersistenceProvider;
        static WorkflowInstance Instance;

        Establish context;

        public DataIO()
        {
            context = EstablishContext;
        }

        protected virtual void ConfigureWorkflow(IServiceCollection services)
        {
            services.AddWorkflow();
        }

        void EstablishContext()
        {
            //setup dependency injection
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            ConfigureWorkflow(services);
            
            var serviceProvider = services.BuildServiceProvider();

            //config logging
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            loggerFactory.AddConsole(LogLevel.Debug);

            var registry = serviceProvider.GetService<IWorkflowRegistry>();            
            registry.RegisterWorkflow(new DataIOWorkflow());

            PersistenceProvider = serviceProvider.GetService<IPersistenceProvider>();
            Host = serviceProvider.GetService<IWorkflowHost>();
            Host.Start();            
        }

        Because of = () =>
        {
            WorkflowId = Host.StartWorkflow("DataIOWorkflow", new DataIODataClass() { Value1 = 2, Value2 = 3 }).Result;
            Instance = PersistenceProvider.GetWorkflowInstance(WorkflowId).Result;
            int counter = 0;
            while ((Instance.Status == WorkflowStatus.Runnable) && (counter < 60))
            {
                System.Threading.Thread.Sleep(500);
                counter++;
                Instance = PersistenceProvider.GetWorkflowInstance(WorkflowId).Result;                
            }
        };

        Behaves_like<DataIOBehavior> a_data_io_workflow;

        Cleanup after = () =>
        {
            Host.Stop();
        };


    }
}
