// Bài 6: JavaScript Cơ bản

// =====================
// 1. Biến và Kiểu dữ liệu
// =====================
function demoVariables() {
    // Khai báo biến với let, const, var
    let name = "Nguyễn Văn A";      // String
    const age = 25;                  // Number (không thay đổi được)
    let isStudent = true;            // Boolean
    let scores = [8, 9, 7, 10];      // Array
    let person = {                   // Object
        name: "Nguyễn Văn A",
        age: 25,
        city: "Hà Nội"
    };
    let nothing = null;              // Null
    let notDefined;                  // Undefined

    let result = `
        <strong>Các kiểu dữ liệu trong JavaScript:</strong><br>
        - String: "${name}" (typeof: ${typeof name})<br>
        - Number: ${age} (typeof: ${typeof age})<br>
        - Boolean: ${isStudent} (typeof: ${typeof isStudent})<br>
        - Array: [${scores}] (isArray: ${Array.isArray(scores)})<br>
        - Object: ${JSON.stringify(person)} (typeof: ${typeof person})<br>
        - Null: ${nothing} (typeof: ${typeof nothing})<br>
        - Undefined: ${notDefined} (typeof: ${typeof notDefined})
    `;

    document.getElementById("variableResult").innerHTML = result;
}

// =====================
// 2. Hàm và Toán tử
// =====================
function calculate(operation) {
    let num1 = parseFloat(document.getElementById("num1").value);
    let num2 = parseFloat(document.getElementById("num2").value);
    let result;

    // Kiểm tra input hợp lệ
    if (isNaN(num1) || isNaN(num2)) {
        document.getElementById("calcResult").innerHTML = "Vui lòng nhập số hợp lệ!";
        document.getElementById("calcResult").classList.add("error");
        return;
    }

    // Sử dụng switch-case
    switch (operation) {
        case 'add':
            result = num1 + num2;
            break;
        case 'subtract':
            result = num1 - num2;
            break;
        case 'multiply':
            result = num1 * num2;
            break;
        case 'divide':
            if (num2 === 0) {
                result = "Không thể chia cho 0!";
            } else {
                result = num1 / num2;
            }
            break;
        default:
            result = "Phép tính không hợp lệ";
    }

    document.getElementById("calcResult").innerHTML = `Kết quả: ${num1} ${getOperator(operation)} ${num2} = <strong>${result}</strong>`;
    document.getElementById("calcResult").classList.remove("error");
}

// Arrow function
const getOperator = (op) => {
    const operators = { add: '+', subtract: '-', multiply: '×', divide: '÷' };
    return operators[op] || '?';
};

// =====================
// 3. Vòng lặp và Điều kiện
// =====================
function demoLoop() {
    let count = parseInt(document.getElementById("loopCount").value);
    let output = "<strong>Kết quả vòng lặp:</strong><br>";

    // For loop
    output += "For loop: ";
    for (let i = 1; i <= count; i++) {
        output += i + " ";
    }
    output += "<br>";

    // While loop với điều kiện
    output += "Số chẵn (while): ";
    let j = 2;
    while (j <= count * 2) {
        output += j + " ";
        j += 2;
    }
    output += "<br>";

    // Array methods
    let numbers = Array.from({ length: count }, (_, i) => i + 1);
    output += `Array.map (bình phương): [${numbers.map(n => n * n)}]<br>`;
    output += `Array.filter (số lẻ): [${numbers.filter(n => n % 2 !== 0)}]<br>`;
    output += `Array.reduce (tổng): ${numbers.reduce((a, b) => a + b, 0)}`;

    document.getElementById("loopResult").innerHTML = output;
}

// =====================
// 4. DOM Manipulation
// =====================
function changeText() {
    let newText = document.getElementById("newText").value;
    if (newText.trim() !== "") {
        document.getElementById("targetText").textContent = newText;
    }
}

function changeColor() {
    let colors = ['#667eea', '#f44336', '#4caf50', '#ff9800', '#9c27b0'];
    let randomColor = colors[Math.floor(Math.random() * colors.length)];
    document.getElementById("targetText").style.color = randomColor;
}

// =====================
// 5. Event Handling - Todo List
// =====================
let todos = [];

function addTodo() {
    let input = document.getElementById("todoInput");
    let text = input.value.trim();

    if (text !== "") {
        todos.push({ id: Date.now(), text: text, completed: false });
        input.value = "";
        renderTodos();
    }
}

function deleteTodo(id) {
    todos = todos.filter(todo => todo.id !== id);
    renderTodos();
}

function toggleTodo(id) {
    todos = todos.map(todo =>
        todo.id === id ? { ...todo, completed: !todo.completed } : todo
    );
    renderTodos();
}

function renderTodos() {
    let list = document.getElementById("todoList");
    list.innerHTML = "";

    todos.forEach(todo => {
        let li = document.createElement("li");
        li.innerHTML = `
            <span style="text-decoration: ${todo.completed ? 'line-through' : 'none'}"
                  onclick="toggleTodo(${todo.id})"
                  style="cursor: pointer">
                ${todo.text}
            </span>
            <button class="delete-btn" onclick="deleteTodo(${todo.id})">Xóa</button>
        `;
        list.appendChild(li);
    });
}

// Thêm event listener cho phím Enter
document.getElementById("todoInput").addEventListener("keypress", function(e) {
    if (e.key === "Enter") {
        addTodo();
    }
});

// =====================
// 6. Form Validation
// =====================
function validateForm(event) {
    event.preventDefault();

    let username = document.getElementById("username").value.trim();
    let email = document.getElementById("email").value.trim();
    let password = document.getElementById("password").value;
    let confirmPassword = document.getElementById("confirmPassword").value;

    let errors = [];

    // Validate username
    if (username.length < 3) {
        errors.push("Tên đăng nhập phải có ít nhất 3 ký tự");
    }

    // Validate email with regex
    let emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
        errors.push("Email không hợp lệ");
    }

    // Validate password
    if (password.length < 6) {
        errors.push("Mật khẩu phải có ít nhất 6 ký tự");
    }

    // Confirm password
    if (password !== confirmPassword) {
        errors.push("Mật khẩu xác nhận không khớp");
    }

    let resultDiv = document.getElementById("formResult");

    if (errors.length > 0) {
        resultDiv.innerHTML = "<strong>Lỗi:</strong><br>" + errors.join("<br>");
        resultDiv.classList.add("error");
    } else {
        resultDiv.innerHTML = `<strong>Đăng ký thành công!</strong><br>
            Username: ${username}<br>
            Email: ${email}`;
        resultDiv.classList.remove("error");
    }

    return false;
}

// Console log khi trang load
console.log("JavaScript Cơ bản - Bài 6 đã được load thành công!");
