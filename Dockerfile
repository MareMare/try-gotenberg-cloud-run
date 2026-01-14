FROM gotenberg/gotenberg:8-cloudrun

# 言語を日本語に
ENV ACCEPT_LANGUAGE=ja-JP

USER root

# フォントのコピー
COPY ./fonts/msgothic.ttc /usr/local/share/fonts/msgothic.ttc

# 日本語パッケージとキャッシュ更新
RUN apt-get update && \
    apt-get install -y fonts-noto-cjk && \
    fc-cache -fv && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Gotenberg のデフォルトユーザーに戻す
USER gotenberg
