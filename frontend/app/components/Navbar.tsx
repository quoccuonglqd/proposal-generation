"use client";

import React, { useEffect, useState } from 'react';
import {
    AppBar,
    Toolbar,
    Typography,
    Button,
    Box,
    Avatar,
    Menu,
    MenuItem,
    IconButton,
    Tooltip
} from '@mui/material';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';

export default function Navbar() {
    const router = useRouter();
    const pathname = usePathname();
    const [user, setUser] = useState<any>(null);
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);

    useEffect(() => {
        const storedUser = localStorage.getItem('user');
        if (storedUser) {
            setUser(JSON.parse(storedUser));
        }
    }, [pathname]);

    const handleMenu = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleClose = () => {
        setAnchorEl(null);
    };

    const handleLogout = () => {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        setUser(null);
        handleClose();
        router.push('/login');
    };

    if (pathname === '/login') return null;

    return (
        <AppBar position="static" color="inherit" elevation={1}>
            <Toolbar>
                <Typography
                    variant="h6"
                    component={Link}
                    href="/"
                    sx={{ flexGrow: 1, textDecoration: 'none', color: 'primary.main', fontWeight: 'bold' }}
                >
                    CHERRY PROPOSAL
                </Typography>

                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                    {user?.roles?.includes('Admin') && (
                        <Button
                            component={Link}
                            href="/admin"
                            variant="outlined"
                            size="small"
                        >
                            Admin Console
                        </Button>
                    )}
                    {user ? (
                        <>
                            <Tooltip title="View profile">
                                <IconButton onClick={handleMenu} sx={{ p: 0 }}>
                                    <Avatar sx={{ bgcolor: 'primary.main' }}>
                                        {user.displayName?.[0] || user.email?.[0] || 'U'}
                                    </Avatar>
                                </IconButton>
                            </Tooltip>
                            <Menu
                                id="menu-appbar"
                                anchorEl={anchorEl}
                                anchorOrigin={{
                                    vertical: 'top',
                                    horizontal: 'right',
                                }}
                                keepMounted
                                transformOrigin={{
                                    vertical: 'top',
                                    horizontal: 'right',
                                }}
                                open={Boolean(anchorEl)}
                                onClose={handleClose}
                            >
                                <Box sx={{ px: 2, py: 1 }}>
                                    <Typography variant="subtitle2">{user.displayName}</Typography>
                                    <Typography variant="caption" color="text.secondary">{user.email}</Typography>
                                </Box>
                                <MenuItem onClick={handleLogout}>Logout</MenuItem>
                            </Menu>
                        </>
                    ) : (
                        <Button component={Link} href="/login" color="primary">
                            Login
                        </Button>
                    )}
                </Box>
            </Toolbar>
        </AppBar>
    );
}
