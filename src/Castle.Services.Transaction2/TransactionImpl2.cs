namespace Castle.Services.Transaction
{
	using System;
	using System.Threading;
	using System.Transactions;
	using Core.Logging;
	using Transaction2.Internal;

	public class TransactionImpl2 : ITransaction2
	{
		private volatile TransactionState _currentState;

		private readonly CommittableTransaction _transaction;
		private readonly TransactionScope _txScope;
		private readonly Activity2 _parentActivity;
		private readonly string _localIdentifier;
		private readonly ILogger _logger;
		private volatile bool _disposed;
		private bool _shouldCommit;

		public TransactionImpl2(System.Transactions.CommittableTransaction transaction, 
			System.Transactions.TransactionScope txScope, 
			Activity2 parentActivity, 
			ILogger logger)
		{
			_transaction = transaction;
			_txScope = txScope;
			_parentActivity = parentActivity;
			_logger = logger;
			_localIdentifier = transaction.TransactionInformation.LocalIdentifier;

			_currentState = TransactionState.Active;

			_parentActivity.Push(this);
		}

		public Transaction Inner { get { return _transaction; } }
		public string LocalIdentifier { get { return _localIdentifier; } }
		public TransactionState State { get { return _currentState; } }

		public TransactionStatus? Status
		{
			get
			{
				return _transaction != null ? 
					_transaction.TransactionInformation.Status : (TransactionStatus?) null;
			}
		}
		
		public void Rollback()
		{
			if (_disposed) throw new ObjectDisposedException("Can't Rollback(). Transaction2 disposed");

			// InternalRollback();

			_shouldCommit = false;
		}

		public void Complete()
		{
			if (_disposed) throw new ObjectDisposedException("Can't Complete(). Transaction2 disposed");

//			InternalComplete();

			_shouldCommit = true;
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			Thread.MemoryBarrier();

//			if (_currentState == TransactionState.Active)
//			{
//				try
//				{
//					InternalRollback();
//				}
//				catch (Exception ex)
//				{
//					_logger.Error("Rollback during dispose error", ex);
//				}
//			}

			if (_shouldCommit)
			{
				_txScope.Complete();
			}

			try
			{
				_txScope.Dispose(); // this does not follow the guidelines, and might throw

				Inner.Dispose();
			}
			finally
			{
				_parentActivity.Pop(this);

				_currentState = TransactionState.Disposed;
			}
		}

		public override string ToString()
		{
			return "tx#" + _localIdentifier;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return InternalEquals((TransactionImpl2)obj);
		}

		public override int GetHashCode()
		{
			return (_localIdentifier != null ? _localIdentifier.GetHashCode() : 0);
		}

		internal bool InternalEquals(TransactionImpl2 other)
		{
			return string.Equals(_localIdentifier, other._localIdentifier);
		}

//		private void InternalRollback()
//		{
//			if (_currentState != TransactionState.Active) throw new InvalidOperationException("Transaction has to be in Active state to Rollback");
//
//			try
//			{
//				if (_logger.IsInfoEnabled)
//					_logger.Info("Rolling back tx#" + _localIdentifier);
//
//				_transaction.Rollback();
//
//				if (_logger.IsInfoEnabled)
//					_logger.Info("Rolled back tx#" + _localIdentifier);
//			}
//			catch (TransactionInDoubtException ex)
//			{
//				_logger.Fatal("Transaction Rollback failed due to InDoubt state. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//			catch (TransactionAbortedException ex)
//			{
//				_logger.Fatal("Transaction Rollback failed due to Aborted state. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//			catch (Exception ex)
//			{
//				_logger.Fatal("Transaction Rollback failed. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//			finally
//			{
//				_currentState = TransactionState.Aborted;
//			}
//		}
//
//		private void InternalComplete()
//		{
//			if (_currentState != TransactionState.Active) throw new InvalidOperationException("Transaction has to be in Active state to Complete");
//
//			try
//			{
//				if (_logger.IsInfoEnabled)
//					_logger.Info("Committing tx#" + _localIdentifier);
//
//				_transaction.Commit();
//
//				_currentState = TransactionState.CommittedOrCompleted;
//
//				if (_logger.IsInfoEnabled)
//					_logger.Info("Committed tx#" + _localIdentifier);
//			}
//			catch (TransactionInDoubtException ex)
//			{
//				_currentState = TransactionState.InDoubt;
//
//				_logger.Fatal("Transaction Commit failed due to InDoubt state. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//			catch (TransactionAbortedException ex)
//			{
//				_currentState = TransactionState.Aborted;
//
//				_logger.Fatal("Transaction Commit failed due to Aborted state. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//			catch (Exception ex)
//			{
//				InternalRollback();
//
//				_currentState = TransactionState.Aborted;
//
//				_logger.Fatal("Transaction Complete failed. tx#" + _localIdentifier, ex);
//
//				throw;
//			}
//		}
	}
}