namespace VkAllocatorSystem;

internal class RwLock : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();

    public void Dispose()
    {
        _lock.Dispose();
    }

    public ReadLockToken ReadLock()
    {
        return new ReadLockToken(_lock);
    }

    public WriteLockToken WriteLock()
    {
        return new WriteLockToken(_lock);
    }

    public UpgradeLockToken UpgradeLock()
    {
        return new UpgradeLockToken(_lock);
    }

    public struct WriteLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public WriteLockToken(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            @lock.EnterWriteLock();
        }

        public void Dispose()
        {
            if (_lock.IsWriteLockHeld) _lock.ExitWriteLock();
        }
    }

    public struct ReadLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public ReadLockToken(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            @lock.EnterReadLock();
        }

        public void Dispose()
        {
            if (_lock.IsReadLockHeld) _lock.ExitReadLock();
        }
    }

    public struct UpgradeLockToken : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;

        public UpgradeLockToken(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
            @lock.EnterUpgradeableReadLock();
        }

        public void Dispose()
        {
            if (_lock.IsUpgradeableReadLockHeld)
                _lock.ExitUpgradeableReadLock();
        }
    }
}