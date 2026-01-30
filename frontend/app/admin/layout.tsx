"use client";

import React from 'react';
import {
    Box,
    Container,
    Drawer,
    List,
    ListItem,
    ListItemButton,
    ListItemIcon,
    ListItemText,
    Toolbar,
    AppBar,
    Typography,
    Divider,
    Stack
} from '@mui/material';
import {
    Public as RegionIcon,
    Category as ServiceIcon,
    Layers as TemplateIcon,
    Dashboard as DashboardIcon,
    People as UserIcon,
    TrendingUp as RateIcon
} from '@mui/icons-material';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

const drawerWidth = 240;

const menuItems = [
    { text: 'Dashboard', icon: <DashboardIcon />, href: '/admin' },
    { text: 'Regions', icon: <RegionIcon />, href: '/admin/regions' },
    { text: 'Services & Pricing', icon: <ServiceIcon />, href: '/admin/services' },
    { text: 'Proposal Sections', icon: <TemplateIcon />, href: '/admin/sections' },
    { text: 'Templates', icon: <TemplateIcon />, href: '/admin/templates' },
    { text: 'Exchange Rates', icon: <RateIcon />, href: '/admin/exchange-rates' },
    { text: 'Users', icon: <UserIcon />, href: '/admin/users' },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();

    return (
        <Box sx={{ display: 'flex' }}>
            <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
                <Toolbar>
                    <Typography variant="h6" noWrap component="div">
                        Cherry Admin Console
                    </Typography>
                </Toolbar>
            </AppBar>
            <Drawer
                variant="permanent"
                sx={{
                    width: drawerWidth,
                    flexShrink: 0,
                    [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: 'border-box' },
                }}
            >
                <Toolbar />
                <Box sx={{ overflow: 'auto' }}>
                    <List>
                        {menuItems.map((item) => (
                            <ListItem key={item.text} disablePadding>
                                <ListItemButton
                                    component={Link}
                                    href={item.href}
                                    selected={pathname === item.href}
                                >
                                    <ListItemIcon>{item.icon}</ListItemIcon>
                                    <ListItemText primary={item.text} />
                                </ListItemButton>
                            </ListItem>
                        ))}
                    </List>
                    <Divider />
                </Box>
            </Drawer>
            <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
                <Toolbar />
                <Container maxWidth="lg">
                    {children}
                </Container>
            </Box>
        </Box>
    );
}
