import os
import sys
import subprocess
import openai
from openai import ChatCompletion  # 直接インポートすることで新インターフェースを利用

def get_diff() -> str:
    """
    最新コミットとその1つ前のコミット間の差分を取得する。
    HEAD~1 が存在しない場合は、空ツリーと HEAD の差分を取得する。
    """
    try:
        # HEAD~1 の存在チェック
        subprocess.check_output(["git", "rev-parse", "HEAD~1"], universal_newlines=True)
        # HEAD~1 が存在する場合
        diff = subprocess.check_output(
            ["git", "diff", "HEAD~1", "HEAD"],
            universal_newlines=True
        )
    except subprocess.CalledProcessError:
        # HEAD~1 が存在しない場合は、空ツリーとの diff を取得
        # 4b825dc642cb6eb9a060e54bf8d69288fbee4904 は Git の空ツリーのハッシュ
        diff = subprocess.check_output(
            ["git", "diff", "4b825dc642cb6eb9a060e54bf8d69288fbee4904", "HEAD"],
            universal_newlines=True
        )
    return diff

def review_code(diff: str) -> str:
    """
    GPT-4 を用いてコードの差分をレビューし、Markdown 形式で結果を返す
    """
    prompt = f"""
You are an expert code reviewer. Please review the following code diff and provide a detailed review covering:
- Positive aspects
- Areas for improvement
- Potential bugs or issues

Code Diff:
{diff}

Please output your review in Markdown format.
"""
    # 新しいインターフェースを利用して ChatCompletion を呼び出す
    response = ChatCompletion.create(
        model="gpt-4",
        messages=[
            {"role": "system", "content": "You are an expert code reviewer."},
            {"role": "user", "content": prompt}
        ]
    )
    return response['choices'][0]['message']['content']

def save_review(review: str):
    """
    レビュー結果を docs/review_report.md に保存する
    """
    os.makedirs("docs", exist_ok=True)
    file_path = "docs/review_report.md"
    with open(file_path, "w", encoding="utf-8") as file:
        file.write(review)
    print(f"Review report saved to {file_path}")

def main():
    if not os.getenv("OPENAI_API_KEY"):
        print("Error: OPENAI_API_KEY is not set in environment variables.")
        sys.exit(1)
    
    print("Obtaining git diff...")
    diff = get_diff()
    
    print("Generating code review via OpenAI...")
    review = review_code(diff)
    
    save_review(review)
    print("✅ Code review completed successfully!")

if __name__ == "__main__":
    main()
