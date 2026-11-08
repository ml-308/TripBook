using System.Windows;
using System.Windows.Controls;

namespace TripBook
{
    /// <summary>时间轴模板选择器：普通乘降事件用 EntryTemplate，时间标记行用 MarkerTemplate。</summary>
    public class TimelineSelector : DataTemplateSelector
    {
        public DataTemplate EntryTemplate { get; set; }
        public DataTemplate MarkerTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var vm = item as EntryVm;
            if (vm != null && vm.IsMarker) return MarkerTemplate;
            return EntryTemplate;
        }
    }
}
