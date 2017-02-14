﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Machinarium.ActorModel
{
    public class ActorCore
    {   
        private ActionBlock<Action> block;

        public ActorCore(ActorContextOptions contextOption = ActorContextOptions.OwnContext)
        {
            Action<Action> msgHandler = a =>
            {
                SynchronizationContext.SetSynchronizationContext(Context);
                a();
                AfterHandler();
            };

            if (contextOption == ActorContextOptions.OwnContext)
            {
                block = new ActionBlock<Action>(msgHandler);
                Context = new ActorSynchronizationContext(block);
            }
            else if (contextOption == ActorContextOptions.InheritContext)
            {
                var options = new ExecutionDataflowBlockOptions() { TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext() };
                block = new ActionBlock<Action>(msgHandler, options);

                this.Context = SynchronizationContext.Current;
            }
        }

        protected SynchronizationContext Context { get; private set; }

        protected virtual void AfterHandler() { }

        public void Enqueue(Action actorAction)
        {
            block.Post(actorAction);
        }
    }

    public enum ActorContextOptions
    {
        OwnContext,
        InheritContext
    }

    internal class ActorSynchronizationContext : SynchronizationContext
    {
        private ActionBlock<Action> block;

        public ActorSynchronizationContext(ActionBlock<Action> block)
        {
            this.block = block;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            block.Post(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotImplementedException();
        }
    }
}