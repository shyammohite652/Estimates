import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    sessionStorage.setItem('ce.session', JSON.stringify({
      token: 'abc.def.ghi',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: '1', email: 'e@x', displayName: 'E', role: 'Estimator' },
    }));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    sessionStorage.clear();
  });

  it('attaches the bearer token to API requests', () => {
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
    http.get('/api/projects').subscribe();
    expect(backend.expectOne('/api/projects').request.headers.get('Authorization')).toBe('Bearer abc.def.ghi');
  });

  it('never sends the token to other hosts', () => {
    http.get('https://example.com/data').subscribe();
    expect(backend.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });
});
