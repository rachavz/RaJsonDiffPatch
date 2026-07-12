namespace RaJsonDiffPatch
{
    /// <summary>
    /// Provides an abstract base class for applying JSON Patch operations to a document of type <typeparamref name="TDoc"/>.
    /// </summary>
    /// <typeparam name="TDoc">The type of document to patch.</typeparam>
    public abstract class AbstractPatcher<TDoc> where TDoc : class 
    {

        /// <summary>
        /// Applies all operations in the specified <see cref="PatchDocument"/> to the target document.
        /// </summary>
        /// <param name="target">
        /// The target document to patch. Passed by reference because the root object may be replaced.
        /// </param>
        /// <param name="document">The patch document containing the operations to apply.</param>
        public virtual void Patch(ref TDoc target, PatchDocument document)
        {
            foreach (var operation in document.Operations)
            {
                target = ApplyOperation(operation, target);
            }
        }

        /// <summary>
        /// Applies a single operation to the target document and returns the (potentially new) root document.
        /// </summary>
        /// <param name="operation">The operation to apply.</param>
        /// <param name="target">The target document.</param>
        /// <returns>The original or a new root document after the operation is applied.</returns>
        public virtual TDoc ApplyOperation(Operation operation, TDoc target)
        {
            switch (operation)
            {
                case AddOperation add:
                    Add(add, target);
                    break;
                case CopyOperation copy:
                    Copy(copy, target);
                    break;
                case MoveOperation move:
                    Move(move, target);
                    break;
                case RemoveOperation remove:
                    Remove(remove, target);
                    break;
                case ReplaceOperation replace:
                    target = Replace(replace, target) ?? target;
                    break;
                case TestOperation test:
                    Test(test, target);
                    break;
            }
            return target;
        }

        /// <summary>
        /// When overridden, applies an "add" operation to the target document.
        /// </summary>
        /// <param name="operation">The add operation to apply.</param>
        /// <param name="target">The target document.</param>
        protected abstract void Add(AddOperation operation, TDoc target);

        /// <summary>
        /// When overridden, applies a "copy" operation to the target document.
        /// </summary>
        /// <param name="operation">The copy operation to apply.</param>
        /// <param name="target">The target document.</param>
        protected abstract void Copy(CopyOperation operation, TDoc target);

        /// <summary>
        /// When overridden, applies a "move" operation to the target document.
        /// </summary>
        /// <param name="operation">The move operation to apply.</param>
        /// <param name="target">The target document.</param>
        protected abstract void Move(MoveOperation operation, TDoc target);

        /// <summary>
        /// When overridden, applies a "remove" operation to the target document.
        /// </summary>
        /// <param name="operation">The remove operation to apply.</param>
        /// <param name="target">The target document.</param>
        protected abstract void Remove(RemoveOperation operation, TDoc target);

        /// <summary>
        /// When overridden, applies a "replace" operation to the target document and returns the new root, or <c>null</c> if the root did not change.
        /// </summary>
        /// <param name="operation">The replace operation to apply.</param>
        /// <param name="target">The target document.</param>
        /// <returns>A new root document if the root was replaced; otherwise <c>null</c>.</returns>
        protected abstract TDoc Replace(ReplaceOperation operation, TDoc target);

        /// <summary>
        /// When overridden, applies a "test" operation to the target document.
        /// </summary>
        /// <param name="operation">The test operation to apply.</param>
        /// <param name="target">The target document.</param>
        protected abstract void Test(TestOperation operation, TDoc target);
        
    }
}