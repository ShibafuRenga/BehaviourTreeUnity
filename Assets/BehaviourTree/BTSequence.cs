namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 顺序：子节点从左到右全部 Success 才 Success；任一 Failure 则 Failure；遇 Running 则保持下标并返回 Running。
    /// </summary>
    [BTNodeType("sequence", "顺序 Sequence")]
    public class BTSequence : BTComposite
    {
        private int _index;

        public BTSequence(string name = null) : base(name)
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
                    case BTStatus.Failure:
                        _index = 0;
                        return BTStatus.Failure;
                    case BTStatus.Success:
                        _index++;
                        break;
                }
            }

            _index = 0;
            return BTStatus.Success;
        }

        protected override void ResetMemory() => _index = 0;
    }
}
