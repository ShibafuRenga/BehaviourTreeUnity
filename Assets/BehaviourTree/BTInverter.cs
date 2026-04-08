namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 取反：Success ↔ Failure，Running 不变。
    /// </summary>
    [BTNodeType("inverter", "取反 Inverter", EditorMenuPath = "Composite")]
    public class BTInverter : BTDecorator
    {
        public BTInverter(BTNode child, string name = null) : base(child, name ?? "Inverter")
        {
        }

        protected override BTStatus TickCore(BTContext context)
        {
            if (Child == null)
                return BTStatus.Success;

            var s = Child.Tick(context);
            return s switch
            {
                BTStatus.Success => BTStatus.Failure,
                BTStatus.Failure => BTStatus.Success,
                BTStatus.Running => BTStatus.Running,
                _ => BTStatus.Failure
            };
        }
    }
}
