using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditorInternal;

namespace Utility
{
    public class FSM
    {
        public List<FSMState> States = new();
        public FSMState State;

        protected FSMState _defaultState = null;

        public virtual FSMState DefaultState
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

        public int AddState(FSMState state)
        {
            int code = States.Count;
            States.Add(state);
            return code;
        }

        public FSMState GetState(int stateCode)
        {
            return States[stateCode];
        }

        public int GetStateCode(FSMState state)
        {
            return States.IndexOf(state);
        }

        public virtual void SwitchState(FSMState nextState)
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

        public virtual FSMState NextState()
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
    public class FSMState
    {
        public FSM FSM;


        public FSMState(FSM fsm)
        {
            FSM = fsm;
        }
        public virtual void StateStart()
        {

        }

        public virtual void StateEnd()
        {

        }

        public virtual FSMState StateAction()
        {
            return FSM.DefaultState;
        }
    }
}