using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using System.Text;
using UnityEngine;

namespace Unity.Android.Logcat
{
    [Serializable]
    class FilterOptions
    {
        [SerializeField]
        protected string m_Filter;
        [SerializeField]
        protected bool m_UseRegularExpressions;
        [SerializeField]
        protected bool m_MatchCase;

        public FilterOptions()
        {
            m_Filter = string.Empty;
        }

        public FilterOptions(FilterOptions options)
        {
            m_Filter = options.Filter;
            m_MatchCase = options.MatchCase;
            m_UseRegularExpressions = options.UseRegularExpressions;
            OnUpdate();
        }

        public Action OnFilterChanged { set; get; }

        public string Filter
        {
            set
            {
                if (value == null)
                    throw new NullReferenceException("Filter");
                if (m_Filter.Equals(value))
                    return;
                m_Filter = value;
                OnUpdate();
            }
            get { return m_Filter; }
        }

        public bool UseRegularExpressions
        {
            set
            {
                if (m_UseRegularExpressions == value)
                    return;
                m_UseRegularExpressions = value;
                OnUpdate();
            }

            get
            {
                return m_UseRegularExpressions;
            }
        }

        public bool MatchCase
        {
            set
            {
                if (m_MatchCase == value)
                    return;
                m_MatchCase = value;
                OnUpdate();
            }
            get
            {
                return m_MatchCase;
            }
        }

        protected virtual void OnUpdate()
        {
            OnFilterChanged?.Invoke();
        }

        public virtual bool IsValid => true;
    }

    class LogcatFilterOptions : FilterOptions
    {
        private Regex m_CachedRegex;

        public LogcatFilterOptions(FilterOptions options)
            : base(options)
        {
        }

        protected override void OnUpdate()
        {
            if (m_UseRegularExpressions)
            {
                try
                {
                    var options = RegexOptions.Compiled;
                    if (!m_MatchCase)
                        options |= RegexOptions.IgnoreCase;
                    m_CachedRegex = new Regex(m_Filter, options);
                }
                catch (Exception ex)
                {
                    AndroidLogcatInternalLog.Log($"Input search filter '{m_Filter}' is not a valid regular expression.\n{ex}");

                    m_CachedRegex = null;
                }
            }
            else
            {
                m_CachedRegex = null;
            }

            OnFilterChanged?.Invoke();
        }

        public bool Matches(string message)
        {
            if (m_UseRegularExpressions)
            {
                // Our regex was invalid, accept all messages
                if (m_CachedRegex == null)
                    return true;
                return m_CachedRegex.Match(message).Success;
            }
            else
            {
                if (m_MatchCase)
                    return message.IndexOf(m_Filter, StringComparison.InvariantCulture) != -1;
                return message.IndexOf(m_Filter, StringComparison.InvariantCultureIgnoreCase) != -1;
            }
        }

        public override bool IsValid => !m_UseRegularExpressions || m_CachedRegex != null;
    }

}
