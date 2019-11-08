// Sample execution
// qrk quic.tech:4433

use std::env;

fn main() {
    for argument in env::args() {
        println!("{}", argument);
    }
}
