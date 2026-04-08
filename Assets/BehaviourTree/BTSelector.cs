namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 选择：子节点从左到右，任一 Success 即 Success；全部 Failure 才 Failure；遇 Running 则保持下标并返回 Running。
    /// </summary>
    [BTNodeType("selector", "选择 Selector", EditorMenuPath = "Composite")]
    public class BTSelector : BTComposite
    {
        private int _index;

        public BTSelector(string name = null) : base(name)
        {
        }

        protected override BTStatus TickCore(BTContext context)
        {
            while (_index < Children.Count)
            {
                var status = Children[_index].Tick(context);
                switch (status)
                {
                    case BTStatus.Running:
                        return BTStatus.Running;
                    case BTStatus.Success:
                        _index = 0;
                        return BTStatus.Success;
                    case BTStatus.Failure:
                        _index++;
                        break;
                }
            }

            _index = 0;
            return BTStatus.Failure;
        }

        protected override void ResetMemory() => _index = 0;
    }
}
