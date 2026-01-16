namespace NodeRed.Editor.Services;

/// <summary>
/// Translated from: @node-red/editor-client/src/js/user.js
/// User management and authentication state
/// </summary>
public class User
{
    private readonly Events _events;
    private UserInfo? _currentUser;
    private bool _isLoggedIn;

    public User(Events events)
    {
        _events = events;
    }

    public class UserInfo
    {
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? Image { get; set; }
        public bool Anonymous { get; set; }
        public List<string> Permissions { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// Get the current logged-in user
    /// </summary>
    public UserInfo? CurrentUser => _currentUser;

    /// <summary>
    /// Check if a user is logged in
    /// </summary>
    public bool IsLoggedIn => _isLoggedIn;

    /// <summary>
    /// Set the current user
    /// </summary>
    public void Login(UserInfo user)
    {
        _currentUser = user;
        _isLoggedIn = !user.Anonymous;
        _events.Emit("user:login", user);
    }

    /// <summary>
    /// Log out the current user
    /// </summary>
    public void Logout()
    {
        var previousUser = _currentUser;
        _currentUser = null;
        _isLoggedIn = false;
        _events.Emit("user:logout", previousUser);
    }

    /// <summary>
    /// Check if the user has a specific permission
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (_currentUser == null)
            return false;

        // Wildcard permission
        if (_currentUser.Permissions.Contains("*"))
            return true;

        // Check exact match
        if (_currentUser.Permissions.Contains(permission))
            return true;

        // Check prefix match (e.g., "flows.write" matches "flows.*")
        var parts = permission.Split('.');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            var prefix = string.Join(".", parts.Take(i + 1)) + ".*";
            if (_currentUser.Permissions.Contains(prefix))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get a user setting
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default)
    {
        if (_currentUser?.Settings.TryGetValue(key, out var value) == true)
        {
            if (value is T typedValue)
                return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Set a user setting
    /// </summary>
    public void SetSetting(string key, object value)
    {
        if (_currentUser != null)
        {
            _currentUser.Settings[key] = value;
            _events.Emit("user:settings:changed", new { key, value });
        }
    }
}
