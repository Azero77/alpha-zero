const initParams = { headers: new Headers() };
const headers = new Headers(initParams.headers);
headers.set('Authorization', 'Bearer abc');
initParams.headers = headers;
const req = new Request('http://localhost', initParams);
console.log('Authorization:', req.headers.get('Authorization'));
