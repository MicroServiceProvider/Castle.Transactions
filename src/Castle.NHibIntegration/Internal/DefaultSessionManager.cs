﻿namespace Castle.NHibIntegration.Internal
{
	using System;
	using System.Transactions;
	using Core.Logging;
	using MicroKernel.Facilities;
	using NHibernate;
	using Services.Transaction;

	public class DefaultSessionManager : ISessionManager
	{
		private readonly ISessionStore _sessionStore;
		private readonly ISessionFactoryResolver _factoryResolver;
		private readonly ITransactionManager2 _transactionManager;
		private FlushMode defaultFlushMode = FlushMode.Auto;

		public DefaultSessionManager(ISessionStore sessionStore, 
			ISessionFactoryResolver factoryResolver, 
			ITransactionManager2 transactionManager)
		{
			_sessionStore = sessionStore;
			_factoryResolver = factoryResolver;
			_transactionManager = transactionManager;

			Logger = NullLogger.Instance;
		}

		public ILogger Logger { get; set; }

		public ISession OpenSession()
		{
			return OpenSession(NhConstants.DefaultAlias);
		}

		public ISession OpenSession(string alias)
		{
			if (string.IsNullOrEmpty(alias)) throw new ArgumentNullException("alias");

			ITransaction2 currentTransaction = _transactionManager.CurrentTransaction;

			SessionDelegate wrapped = _sessionStore.FindCompatibleSession(alias);

			if (wrapped == null) // || (currentTransaction != null && !wrapped.Transaction.IsActive))
			{
				var session = InternalCreateSession(alias);

				if (Logger.IsDebugEnabled)
					Logger.Debug("Created Session = [" + session.GetSessionImplementation().SessionId + "]");

				wrapped = WrapSession(alias, session, currentTransaction, canClose: currentTransaction == null);
				EnlistIfNecessary(currentTransaction, wrapped, weAreSessionOwner: true);

				// _sessionStore.Store(alias, wrapped);
				wrapped.Store();

				if (Logger.IsDebugEnabled)
					Logger.Debug("Wrapped Session = [" + wrapped.GetSessionImplementation().SessionId + "]");
			}
			else
			{
				if (Logger.IsDebugEnabled)
					Logger.Debug("Re-wrapping Session = [" + wrapped.GetSessionImplementation().SessionId + "]");

				wrapped = WrapSession(alias, wrapped.InnerSession, null, canClose: false);
				EnlistIfNecessary(currentTransaction, wrapped, weAreSessionOwner: false);
			}

			return wrapped;
		}

		/// <summary>
		/// The flushmode the created session gets
		/// </summary>
		/// <value></value>
		public FlushMode DefaultFlushMode
		{
			get { return defaultFlushMode; }
			set { defaultFlushMode = value; }
		}

		private void EnlistIfNecessary(ITransaction2 transaction, SessionDelegate session, bool weAreSessionOwner)
		{
			if (transaction == null) return;

			if (weAreSessionOwner /*&& session.Transaction.IsActive*/)
			{
				Logger.Debug("Enlisted Session " + session.GetSessionImplementation().SessionId);

				var ue = new UnregisterEnlistment(Logger, session);

				transaction.Inner.EnlistVolatile(ue, EnlistmentOptions.EnlistDuringPrepareRequired);
			}
		}

		private SessionDelegate WrapSession(string alias, ISession session, 
											ITransaction2 currentTransaction, 
											bool canClose)
		{
			var sessdelegate = new SessionDelegate(alias, canClose, session, _sessionStore, this.Logger.CreateChildLogger("Session"));

			if (currentTransaction != null)
			{
				sessdelegate.InternalBeginTransaction();
			}

			return sessdelegate;
		}

		private ISession InternalCreateSession(string @alias)
		{
			ISessionFactory sessionFactory = _factoryResolver.GetSessionFactory(@alias);

			if (sessionFactory == null)
			{
				throw new FacilityException("No ISessionFactory implementation " +
											"associated with the given alias: " + @alias);
			}

			ISession session;

			{
				session = sessionFactory.OpenSession();
			}

			session.FlushMode = defaultFlushMode;

			return session;
		}
	}
}