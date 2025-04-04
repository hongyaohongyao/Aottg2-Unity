using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditorInternal;

namespace Utility
{
    public class Automaton
    {
        public List<AutomationState> States = new();
        public AutomationState State;

        protected AutomationState _defaultState = null;

        public virtual AutomationState DefaultState
        {
            get
            {
                return _defaultState;
            }
            set
            {
                _defaultState = value;
            }
        }

        public void InitStates()
        {
            foreach (var state in States)
            {
                state.StateEnd();
            }
            State = null;
            States.Clear();
        }

        public int AddState(AutomationState state)
        {
            int code = States.Count;
            States.Add(state);
            return code;
        }

        public AutomationState GetState(int stateCode)
        {
            return States[stateCode];
        }

        public int GetStateCode(AutomationState state)
        {
            return States.IndexOf(state);
        }

        public virtual void SwitchState(AutomationState nextState)
        {
            if (State == nextState)
            {
                return;
            }
            if (nextState != null)
            {
                nextState.StateStart();
            }
            var oldState = State;
            State = nextState;
            if (oldState != null)
            {
                oldState.StateEnd();
            }
        }

        public virtual AutomationState Action()
        {
            if (State == null)
            {
                State = DefaultState;
            }
            if (State != null)
            {
                SwitchState(State.StateAction());
            }
            return State;
        }
    }
    public class AutomationState
    {
        public Automaton Automation;


        public AutomationState(Automaton automaton)
        {
            Automation = automaton;
        }
        public virtual void StateStart()
        {

        }

        public virtual void StateEnd()
        {

        }

        public virtual AutomationState StateAction()
        {
            return Automation.DefaultState;
        }
    }
}