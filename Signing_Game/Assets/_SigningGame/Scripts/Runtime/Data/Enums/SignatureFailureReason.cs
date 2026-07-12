namespace Data.Enums {
    public enum SignatureFailureReason {
        None = 0,

        EmptyAttempt = 1,
        NoUsableStrokes = 2,
        TooFewPoints = 3,
        TooManyPoints = 4,
        StrokeTooShort = 5,

        BelowSimilarityThreshold = 6
    }
}