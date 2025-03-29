using UnityEngine;
using Characters;
using Utility;
using Controllers.HumanAIActions;

namespace Controllers
{
    class HumanAIFSM : FSM
    {
        protected HumanAIController _controller;
        public void Init(HumanAIController controller)
        {
            _controller = controller;
            AddState(null);
            AddState(new HumanAIFinding(this, controller));
            AddState(new HumanAILocking(this, controller));
            AddState(new HumanAIFollowing(this, controller));
        }
    }

    public enum HumanAIStates
    {
        None,
        Finding,
        Locking,
        Following
    }
}