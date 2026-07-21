namespace Data.Enums {
    public enum SignatureFailureReason {
        None = 0,

        EmptyAttempt = 1,
        NoUsableStrokes = 2,
        TooFewPoints = 3,
        TooManyPoints = 4,
        BelowSimilarityThreshold = 5
    }
}