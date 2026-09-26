namespace GameLogic.Navigation
{
    /// <summary>
    /// Describes the result of one synchronous navigation request.
    /// </summary>
    public enum EPathQueryStatus
    {
        /// <summary>The output path reaches the requested destination.</summary>
        Success,

        /// <summary>No complete navigation map is currently bound.</summary>
        MapUnavailable,

        /// <summary>The request cannot be evaluated because an argument is invalid.</summary>
        InvalidRequest,

        /// <summary>The actual start position cannot safely join the navigation map.</summary>
        InvalidStart,

        /// <summary>The exact requested destination cannot be reached safely.</summary>
        InvalidDestination,

        /// <summary>The endpoints are valid, but A* found no connected cell path.</summary>
        NoPath,

        /// <summary>The requested destination is unreachable; the path ends at a nearby reachable position.</summary>
        Approach
    }
}
