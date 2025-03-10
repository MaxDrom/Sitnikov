namespace VkAllocatorSystem;
class RWLock : IDisposable
{
    public struct WriteLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim @lock;
        public WriteLockToken(ReaderWriterLockSlim @lock)
        {
            this.@lock = @lock;
            @lock.EnterWriteLock();
        }
        public void Dispose()
        {
            if (@lock.IsWriteLockHeld)
                @lock.ExitWriteLock();
        }
    }

    public struct ReadLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim @lock;
        public ReadLockToken(ReaderWriterLockSlim @lock)
        {
            this.@lock = @lock;
            @lock.EnterReadLock();
        }
        public void Dispose()
        {
            if (@lock.IsReadLockHeld)
                @lock.ExitReadLock();
        }
    }

    public struct UpgradeLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim @lock;
        public UpgradeLockToken(ReaderWriterLockSlim @lock)
        {
            this.@lock = @lock;
            @lock.EnterUpgradeableReadLock();
        }
        public void Dispose()
        {
            if (@lock.IsUpgradeableReadLockHeld)
                @lock.ExitUpgradeableReadLock();
        }
    }

    private readonly ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

    public ReadLockToken ReadLock() => new ReadLockToken(@lock);
    public WriteLockToken WriteLock() => new WriteLockToken(@lock);
    public UpgradeLockToken UpgradeLock() => new UpgradeLockToken(@lock);

    public void Dispose() => @lock.Dispose();
}