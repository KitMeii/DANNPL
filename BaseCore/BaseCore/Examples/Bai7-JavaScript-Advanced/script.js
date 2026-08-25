// Bài 7: JavaScript Nâng cao

// API Base URL (thay đổi khi cần)
const API_BASE = 'http://localhost:5001';
const AUTH_API = 'http://localhost:5002';

// =====================
// 1. ES6+ Features Demo
// =====================
function demoES6() {
    let result = '<strong>ES6+ Features:</strong><br><br>';

    // 1. Template Literals
    const name = 'JavaScript';
    const version = 'ES6';
    result += `1. Template Literals: Xin chào ${name} ${version}!<br><br>`;

    // 2. Destructuring
    const person = { firstName: 'Nguyễn', lastName: 'Văn A', age: 25 };
    const { firstName, lastName } = person;
    result += `2. Destructuring: ${firstName} ${lastName}<br><br>`;

    // 3. Spread Operator
    const arr1 = [1, 2, 3];
    const arr2 = [...arr1, 4, 5];
    result += `3. Spread Operator: [${arr2}]<br><br>`;

    // 4. Arrow Functions
    const multiply = (a, b) => a * b;
    result += `4. Arrow Function: multiply(3, 4) = ${multiply(3, 4)}<br><br>`;

    // 5. Default Parameters
    const greet = (name = 'Guest') => `Hello, ${name}!`;
    result += `5. Default Params: ${greet()} và ${greet('User')}<br><br>`;

    // 6. Array Methods
    const numbers = [1, 2, 3, 4, 5];
    result += `6. Array Methods:<br>
        - map: [${numbers.map(n => n * 2)}]<br>
        - filter: [${numbers.filter(n => n > 2)}]<br>
        - find: ${numbers.find(n => n > 3)}<br>
        - reduce: ${numbers.reduce((a, b) => a + b)}<br><br>`;

    // 7. Optional Chaining & Nullish Coalescing
    const user = { profile: { name: 'Test' } };
    result += `7. Optional Chaining: ${user?.profile?.name ?? 'N/A'}<br>`;
    result += `   Nullish Coalescing: ${null ?? 'default value'}`;

    document.getElementById('es6Result').innerHTML = result;
}

// =====================
// 2. Promise Demo
// =====================
function demoPromise() {
    const resultDiv = document.getElementById('promiseResult');
    resultDiv.innerHTML = '<span class="loading">Đang xử lý Promise...</span>';

    // Tạo Promise
    const myPromise = new Promise((resolve, reject) => {
        setTimeout(() => {
            const random = Math.random();
            if (random > 0.3) {
                resolve({ message: 'Promise thành công!', value: random.toFixed(2) });
            } else {
                reject(new Error('Promise thất bại!'));
            }
        }, 1500);
    });

    // Xử lý Promise với .then() và .catch()
    myPromise
        .then(result => {
            resultDiv.innerHTML = `
                <strong>Promise Resolved:</strong><br>
                Message: ${result.message}<br>
                Value: ${result.value}
            `;
            resultDiv.classList.remove('error');
        })
        .catch(error => {
            resultDiv.innerHTML = `<strong>Promise Rejected:</strong><br>${error.message}`;
            resultDiv.classList.add('error');
        });
}

function demoPromiseAll() {
    const resultDiv = document.getElementById('promiseResult');
    resultDiv.innerHTML = '<span class="loading">Đang chạy Promise.all...</span>';

    // Nhiều Promise chạy song song
    const promise1 = new Promise(resolve => setTimeout(() => resolve('Task 1 done'), 1000));
    const promise2 = new Promise(resolve => setTimeout(() => resolve('Task 2 done'), 1500));
    const promise3 = new Promise(resolve => setTimeout(() => resolve('Task 3 done'), 800));

    const startTime = Date.now();

    Promise.all([promise1, promise2, promise3])
        .then(results => {
            const endTime = Date.now();
            resultDiv.innerHTML = `
                <strong>Promise.all Results:</strong><br>
                ${results.map((r, i) => `${i + 1}. ${r}`).join('<br>')}<br><br>
                <em>Tổng thời gian: ${endTime - startTime}ms (chạy song song!)</em>
            `;
        });
}

// =====================
// 3. Async/Await Demo
// =====================
async function demoAsyncAwait() {
    const resultDiv = document.getElementById('asyncResult');
    resultDiv.innerHTML = '<span class="loading">Đang chạy Async/Await...</span>';

    try {
        // Giả lập gọi API tuần tự
        const step1 = await simulateAPI('Bước 1: Khởi tạo', 500);
        const step2 = await simulateAPI('Bước 2: Xác thực', 700);
        const step3 = await simulateAPI('Bước 3: Lấy dữ liệu', 600);
        const step4 = await simulateAPI('Bước 4: Xử lý', 400);

        resultDiv.innerHTML = `
            <strong>Async/Await Results:</strong><br>
            ✅ ${step1}<br>
            ✅ ${step2}<br>
            ✅ ${step3}<br>
            ✅ ${step4}<br><br>
            <em>Tất cả các bước đã hoàn thành!</em>
        `;
    } catch (error) {
        resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        resultDiv.classList.add('error');
    }
}

function simulateAPI(message, delay) {
    return new Promise(resolve => {
        setTimeout(() => resolve(message + ' - Hoàn thành!'), delay);
    });
}

// =====================
// 4. Fetch API - GET Products
// =====================
async function fetchProducts() {
    const resultDiv = document.getElementById('fetchResult');
    const gridDiv = document.getElementById('productGrid');

    resultDiv.innerHTML = '<span class="loading">Đang lấy dữ liệu từ server...</span>';
    gridDiv.innerHTML = '';

    try {
        const response = await fetch(`${API_BASE}/api/products`);

        if (!response.ok) {
            throw new Error(`HTTP Error: ${response.status}`);
        }

        const data = await response.json();
        const products = data.items || data;

        if (products.length === 0) {
            resultDiv.innerHTML = 'Không có sản phẩm nào.';
            return;
        }

        resultDiv.innerHTML = `<strong>Đã lấy ${products.length} sản phẩm:</strong>`;

        products.forEach(product => {
            const card = document.createElement('div');
            card.className = 'product-card';
            card.innerHTML = `
                <h4>${product.name}</h4>
                <p>${product.description || 'Không có mô tả'}</p>
                <p class="price">${formatPrice(product.price)}</p>
                <p>Kho: ${product.stock}</p>
            `;
            gridDiv.appendChild(card);
        });

    } catch (error) {
        resultDiv.innerHTML = `
            <strong>Lỗi:</strong> ${error.message}<br>
            <em>Hãy chắc chắn ProductService đang chạy trên port 5001</em>
        `;
        resultDiv.classList.add('error');
    }
}

async function fetchCategories() {
    const resultDiv = document.getElementById('fetchResult');
    const gridDiv = document.getElementById('productGrid');

    resultDiv.innerHTML = '<span class="loading">Đang lấy danh mục...</span>';
    gridDiv.innerHTML = '';

    try {
        const response = await fetch(`${API_BASE}/api/categories`);
        const categories = await response.json();

        resultDiv.innerHTML = `<strong>Danh mục (${categories.length}):</strong>`;

        categories.forEach(cat => {
            const card = document.createElement('div');
            card.className = 'product-card';
            card.innerHTML = `
                <h4>${cat.name}</h4>
                <p>${cat.description || 'Không có mô tả'}</p>
            `;
            gridDiv.appendChild(card);
        });

    } catch (error) {
        resultDiv.innerHTML = `<strong>Lỗi:</strong> ${error.message}`;
        resultDiv.classList.add('error');
    }
}

// =====================
// 5. POST Request - Login
// =====================
async function login() {
    const username = document.getElementById('loginUsername').value;
    const password = document.getElementById('loginPassword').value;
    const resultDiv = document.getElementById('loginResult');

    resultDiv.innerHTML = '<span class="loading">Đang đăng nhập...</span>';

    try {
        const response = await fetch(`${AUTH_API}/api/auth/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ username, password })
        });

        const data = await response.json();

        if (response.ok) {
            resultDiv.innerHTML = `
                <strong>Đăng nhập thành công!</strong><br>
                User: ${data.username}<br>
                Name: ${data.name}<br>
                Role: ${data.role}<br>
                Token: <code>${data.token.substring(0, 50)}...</code>
            `;
            resultDiv.classList.remove('error');

            // Lưu token vào localStorage
            localStorage.setItem('token', data.token);
            localStorage.setItem('user', JSON.stringify(data));

        } else {
            throw new Error(data.message || 'Login failed');
        }

    } catch (error) {
        resultDiv.innerHTML = `
            <strong>Lỗi đăng nhập:</strong> ${error.message}<br>
            <em>Hãy chắc chắn UserService đang chạy trên port 5003</em>
        `;
        resultDiv.classList.add('error');
    }
}

// Helper function
function formatPrice(price) {
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(price);
}

console.log('Bài 7 - JavaScript Nâng cao đã được load!');
