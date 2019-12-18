CC       = gcc 
CFLAGS   = -Wall -g -O -fPIC 
CXXFLAGS = 
TARGET   = libkcp.so

OBJS     = ikcp.o kcp.o
SRCS     = ikcp.c kcp.cpp

default all: $(OBJS)
	$(CC) -shared -fPIC -o $(TARGET) $(OBJS)
	strip -x $(TARGET)

$(OBJS): $(SRCS)
	$(CC) $(CFLAGS) -c $^

clean:
	rm -f *.o
	rm -f *.so
