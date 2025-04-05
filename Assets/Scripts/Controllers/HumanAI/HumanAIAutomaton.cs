using UnityEngine;
using Characters;
using Utility;
using Controllers.HumanAIActions;

namespace Controllers
{
    class HumanAIAutomaton : Automaton
    {
        protected HumanAIController _controller;
        public void Init(HumanAIController controller)
        {
            _controller = controller;
            AddState(null);
            AddState(new HumanAIFinding(this, controller));
            AddState(new HumanAILocking(this, controller));
            AddState(new HumanAIFollowing(this, controller));
            AddState(new HumanAIFindTarget(this, controller));
            AddState(new HumanAIBattle(this, controller));
        }
    }

    class HumanAIAutomatonState : AutomationState
    {
        protected HumanAIController _controller;
        protected Human _human;

        public HumanAIAutomatonState(Automaton automaton, HumanAIController controller) : base(automaton)
        {
            SetController(controller);
        }

        public virtual void SetController(HumanAIController controller)
        {
            _controller = controller;
            _human = controller.Human;
        }
    }

    public enum HumanAIStates
    {
        None,
        Finding,
        Locking,
        Following,
        FindTarget,
        Battle
    }
}