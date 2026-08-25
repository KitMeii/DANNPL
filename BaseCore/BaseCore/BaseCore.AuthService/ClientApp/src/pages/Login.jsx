import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const Login = () => {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const { login } = useAuth();

  const handleLogin = async () => {
    if (!userName || !password) {
      setError('Please enter username and password');
      return;
    }

    setError('');
    setLoading(true);

    try {
      const result = await login(userName, password);

      if (result.success) {
        navigate('/dashboard', { replace: true });
      } else {
        setError(typeof result.error === 'string' ? result.error : 'Login failed. Please try again.');
        setLoading(false);
      }
    } catch (err) {
      setError('An error occurred. Please try again.');
      setLoading(false);
    }
  };

  const handleKeyPress = (e) => {
    if (e.key === 'Enter') {
      handleLogin();
    }
  };

  return (
    <div className="hold-transition login-page" style={{ minHeight: '100vh', background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}>
      <div className="login-box">
        <div className="login-logo">
          <span style={{ color: 'white' }}><b>BaseCore</b>Sales</span>
        </div>
        <div className="card">
          <div className="card-body login-card-body">
            <p className="login-box-msg">Sign in to start your session</p>

            {error && (
              <div className="alert alert-danger">
                {error}
              </div>
            )}

            <div className="input-group mb-3">
              <input
                type="text"
                className="form-control"
                placeholder="Username"
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                onKeyPress={handleKeyPress}
                autoComplete="username"
              />
              <div className="input-group-append">
                <div className="input-group-text">
                  <span className="fas fa-user"></span>
                </div>
              </div>
            </div>
            <div className="input-group mb-3">
              <input
                type="password"
                className="form-control"
                placeholder="Password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                onKeyPress={handleKeyPress}
                autoComplete="current-password"
              />
              <div className="input-group-append">
                <div className="input-group-text">
                  <span className="fas fa-lock"></span>
                </div>
              </div>
            </div>
            <div className="row">
              <div className="col-8">
                <div className="icheck-primary">
                  <input type="checkbox" id="remember" />
                  <label htmlFor="remember">Remember Me</label>
                </div>
              </div>
              <div className="col-4">
                <button
                  type="button"
                  className="btn btn-primary btn-block"
                  disabled={loading}
                  onClick={handleLogin}
                >
                  {loading ? 'Signing in...' : 'Sign In'}
                </button>
              </div>
            </div>

            <p className="mb-0 mt-3 text-center text-muted">
              <small>Demo: admin / admin123</small>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Login;
