using System;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;

namespace Carnac.UI
{
    /// <summary>
    /// One check box of the "Keys to show" group in Preferences. It reads and writes a single flag of
    /// <see cref="PopupSettings.VisibleKeyCategories"/>, so the other flags are left alone.
    /// </summary>
    public class KeyCategoryOption : NotifyPropertyChanged
    {
        readonly PopupSettings settings;
        readonly KeyCategory category;

        public KeyCategoryOption(PopupSettings settings, KeyCategory category, string label, string description)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;
            this.category = category;
            Label = label;
            Description = description;
        }

        public string Label { get; private set; }

        public string Description { get; private set; }

        public bool IsSelected
        {
            get { return (settings.VisibleKeyCategories & category) == category; }
            set
            {
                settings.VisibleKeyCategories = value
                    ? settings.VisibleKeyCategories | category
                    : settings.VisibleKeyCategories & ~category;
                OnPropertyChanged("IsSelected");
            }
        }
    }
}
