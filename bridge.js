const net = require('net');

const client = new net.Socket();
const PORT = 8964;
const HOST = '127.0.0.1';

const command = {
    Command: process.argv[2] || 'get_layers',
    Args: process.argv[3] ? JSON.parse(process.argv[3]) : {}
};

client.connect(PORT, HOST, () => {
    console.log(`Connected to AutoCAD on ${HOST}:${PORT}`);
    client.write(JSON.stringify(command));
});

let data = '';
client.on('data', (chunk) => {
    data += chunk.toString();
});

client.on('end', () => {
    try {
        const response = JSON.parse(data);
        console.log(JSON.stringify(response, null, 2));
    } catch (e) {
        console.log('Response:', data);
    }
    client.destroy();
});

client.on('error', (err) => {
    console.error('Error:', err.message);
});

setTimeout(() => {
    if (!client.destroyed) {
        console.log('Timeout reached');
        client.destroy();
    }
}, 5000);
